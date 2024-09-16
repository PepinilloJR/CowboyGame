using System;
using System.Net;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Xml;
using AuxMethods;
using juegoOnlineBasico;

namespace Principal
{

    //TODO: asegurarse de crear locks, para el dibujado general, y para la modificacion de las listas que se comparten entre modulos, para
    // ello crear funciones lockeadas para la modificacion de las bibliotecas, y crear funciones lockeadas para el posicionamiento del cursor

    class Program
    {
        static void Main(string[] args)
        {
            Console.CursorVisible = false;
            //Console.SetWindowSize(150, 50);
           // Console.SetBufferSize(150, 50);

            Menu menu = new Menu(">>>");

            while (true)
            {
                menu.DibujarMenu();
                menu.DibujarCursor();

                string key = Console.ReadKey(intercept: true).Key.ToString();


                if (key == "UpArrow" & menu.posCURSOR > (Console.WindowTop / 2) + 5)
                {
                    menu.posCURSOR -= 5;
                }
                else if (key == "DownArrow" & menu.posCURSOR < (Console.WindowTop / 2) + 10)
                {
                    menu.posCURSOR += 5;
                }

                else if (key == "Enter")
                {
                    if (menu.posCURSOR == (Console.WindowTop / 2) + 5)
                    {
                        Console.Clear();
                        //Console.WriteLine("Crear partida!!!");
                        string ip = MenuConexionServer();
                        Server server = new Server(ip, 27015);
                        server.Iniciar();
                    }
                    else if (menu.posCURSOR == (Console.WindowTop / 2) + 10)
                    {
                        Console.Clear();
                        string ip = MenuConexionCliente();

                        Random rnd = new Random();
                        int iD = rnd.Next(0, 1000);
                        object locker = new object();
                        Jugador jugador = new Jugador(20, 20, iD, locker, 6, 3);
                        Escenario escenario = new Escenario(1, 2, 110, 30, ["▓", "▓"]);
                        Dictionary<int, Jugador> jugadores = new Dictionary<int, Jugador>();
                        Dictionary<int, Jugador> jugadoresT = new Dictionary<int, Jugador>(); // una lista para recordar el estado anterior de recibir un nuevo mensaje con los datos de los jugadores

                        List<Bala> balas = new List<Bala>();

                        Cliente cliente = new Cliente(ip, 27015, jugador, jugadores, jugadoresT, balas);
                        Task.Run(cliente.Iniciar);
                       
                        LogicaJuegoC(jugador, jugadores, jugadoresT, balas, locker, escenario);

                        cliente.Disconnect();
                    }

                }
                key = "";
            }



        }


        public static string MenuConexionCliente()
        {
            Console.Write("Ingrese la IP a conectarse: ");
            string ip = Console.ReadLine();
            return ip;
        }

        public static string MenuConexionServer()
        {
            Console.Write("Ingrese la IP con la que se conectaran los jugadores: ");
            string ip = Console.ReadLine();
            return ip;
        }


        public static void LogicaJuegoC(Jugador jugador, Dictionary<int, Jugador> jugadores, Dictionary<int, Jugador> jugadoresT, List<Bala> balas, object locker, Escenario escenario)
        {
            Timer timerBalas = new Timer();
            Timer timerDisparo = new Timer();
            int bPosX = jugador.POSX;
            int bPosY = jugador.POSY;
            Entrada entrada = new Entrada();

            List<int> idJugadoresMuertos = new List<int>(); 

            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            CancellationToken token = cancellationTokenSource.Token;    
            Task.Run(() =>
            {
                AuxMeth.LeerEntrada(entrada, token);
            });
            
            Task.Run(() => { 
                TimerBalas(timerBalas, token);
            });

            Task.Run(() =>
            {
                TimerDisparo(timerDisparo, token);
            });


            escenario.DibujarBordes(locker);
            while (true) {

                Console.CursorVisible = false;
               // Console.SetWindowSize(150, 50);

                bPosX = jugador.POSX;
                bPosY = jugador.POSY;
                //entrada.tecla = Console.ReadKey(intercept: true).Key.ToString();
                if (jugador.MUERTO == false)
                {
                    MoverJugador(jugador, entrada, timerDisparo, escenario);

                    if (bPosX != jugador.POSX || bPosY != jugador.POSY)
                    {
                        jugador.Borrar(bPosX, bPosY);
                    }
                    jugador.Dibujar();

                    if (jugador.VIDA <= 0)
                    {
                        jugador.MUERTO = true;
                    }

                } else if (jugador.ANIMADO == false)
                {
                    jugador.ANIMADO = true;
                    Task.Run(jugador.MorirAnimacion);
                }
                // logica para revivir
                if (entrada.tecla == "R" & jugador.MUERTO)
                {
                    jugador.VIDA = 100;
                    jugador.MUERTO = false;
                    jugador.ANIMADO = false;
                    entrada.tecla = "";
                }

                if (entrada.tecla == "X")
                {
                    cancellationTokenSource.Cancel();
                    break;
                }
                // aca no deberia hacer falta porque no se modifica el coso, pero hay que ir viendo

                foreach (KeyValuePair<int, Jugador> par in jugadores.ToArray()) // el ToArray() es porque la lista de jugadores se pude modificar en pleto forEach, entonces tengo que usar un array temporal
                                                                                // para evitar este error 
                {
                    if (par.Key != jugador.ID)
                    {
                        par.Value.LOCKER = locker;
                        if (jugadoresT[par.Key].POSX != par.Value.POSX || jugadoresT[par.Key].POSY != par.Value.POSY)
                        {

                            par.Value.Borrar(jugadoresT[par.Key].POSX, jugadoresT[par.Key].POSY);

                        }

                        if (par.Value.MUERTO == false)
                        {
                            par.Value.Dibujar();
                        }
                        else if (idJugadoresMuertos.Contains(par.Value.ID) == false)
                        {
                            Task.Run(par.Value.MorirAnimacion);
                            idJugadoresMuertos.Add(par.Value.ID);

                        }
                        
                        if (idJugadoresMuertos.Contains(par.Value.ID))
                        {
                            if (par.Value.MUERTO == false)
                            {
                                idJugadoresMuertos.Remove(par.Value.ID);
                            }
                        }

                    }

                }

                ManejarBalas(balas, locker, timerBalas, jugador, escenario);              
            }
        }

        static void MoverJugador(Jugador jugador, Entrada entrada, Timer timerDisparo, Escenario escenario)
        {
            if (entrada.tecla == "LeftArrow" && jugador.POSX - 1 > escenario.X)
            {
                jugador.POSX -= 1;
                entrada.tecla = "";
                jugador.FACING = 0;

            }
            else if (entrada.tecla == "RightArrow" && jugador.POSX + jugador.ANCHO < escenario.LONGX)
            {
                jugador.POSX += 1;
                entrada.tecla = "";
                jugador.FACING = 1;

            }
            else if (entrada.tecla == "UpArrow" && jugador.POSY - 2 > escenario.Y)
            {
                jugador.POSY -= 1;
                entrada.tecla = "";
                jugador.FACING = 2;

            }
            else if (entrada.tecla == "DownArrow" && jugador.POSY + jugador.ALTO < escenario.LONGY)
            {
                jugador.POSY += 1;
                entrada.tecla = "";
                jugador.FACING = 3;

            }
            else if (entrada.tecla == "Spacebar" & timerDisparo.FINALIZADO)
            {

                Bala bala;
                if (jugador.FACING == 0)
                {
                    bala = new Bala(0, jugador.POSX, jugador.POSY, jugador.ID);
                    jugador.BALAS.Add(bala);
                    // balas.Add(bala);
                }
                else if (jugador.FACING == 1)
                {
                    bala = new Bala(1, jugador.POSX, jugador.POSY, jugador.ID);
                    jugador.BALAS.Add(bala);
                    //  balas.Add(bala);
                }
                else if (jugador.FACING == 2)
                {
                    bala = new Bala(2, jugador.POSX, jugador.POSY, jugador.ID);
                    jugador.BALAS.Add(bala);
                    // balas.Add(bala);
                }
                else if (jugador.FACING == 3)
                {
                    bala = new Bala(3, jugador.POSX, jugador.POSY, jugador.ID);
                    jugador.BALAS.Add(bala);
                    //  balas.Add(bala);
                }
                timerDisparo.FINALIZADO = false;
                entrada.tecla = "";
            }
            
        }

        static void ManejarBalas(List<Bala> balas, object locker, Timer timer, Jugador jugador, Escenario escenario)
        {
            if (timer.FINALIZADO)
            {
                try
                {

                    foreach (Bala bala in balas.ToArray())
                    {
                        //Task.Delay(100);
                        AuxMeth.PosicionarCursor(bala.SPOSX, bala.SPOSY, locker, " ");
                        //Console.CursorLeft = bala.SPOSX;
                        //Console.CursorTop = bala.SPOSY;
                        // Console.Write(" ");


                        if (bala.DIRECCION == 0)
                        {
                            bala.SPOSX -= 1;
                        }
                        if (bala.DIRECCION == 1)
                        {
                            bala.SPOSX += 1;
                        }
                        if (bala.DIRECCION == 2)
                        {
                            bala.SPOSY -= 1;
                        }
                        if (bala.DIRECCION == 3)
                        {
                            bala.SPOSY += 1;
                        }
                        bala.DISTANCIA += 1;


                        if ((bala.SPOSX > jugador.POSX & bala.SPOSX < jugador.POSX + jugador.ANCHO)
                            & (jugador.POSY <= bala.SPOSY & bala.SPOSY <= jugador.POSY + jugador.ALTO))
                        {
                            if (bala.ORIGENID != jugador.ID)
                            {
                                jugador.VIDA = 0;
                            }
                        }

                        if (bala.SPOSX + 1 > escenario.LONGX || bala.SPOSX - 1 < escenario.X
                            || bala.SPOSY - 2 < escenario.Y || bala.SPOSY + 1 > escenario.LONGY)
                        {
                            balas.Remove(bala);
                        } else
                        {
                           AuxMeth.PosicionarCursor(bala.SPOSX, bala.SPOSY, locker, "*", ConsoleColor.Yellow);
                        }
                       
                        
                        //Console.CursorLeft = bala.SPOSX;
                        // Console.CursorTop = bala.SPOSY;
                        //Console.Write("*");

                    }
                    timer.FINALIZADO = false;   
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }
        }

        static async void TimerBalas (Timer timer, CancellationToken token )
        {
            while(true)
            {
                await Task.Delay(10);
                timer.FINALIZADO = true;
                if (token.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        static async void TimerDisparo (Timer timer, CancellationToken token)
        {
            while (true)
            {
                if (timer.FINALIZADO == false)
                {
                    await Task.Delay(1000);
                    timer.FINALIZADO = true;
                } 

                if (token.IsCancellationRequested) {
                    break; 
                }   
            }
        }

    }



    class Escenario
    {
        int x;
        int y;
        int longX;
        int longY;


        string[] limitesSprites;  

        public int X { get { return x; } set { x = value; } }
        public int Y { get { return x; } set { y = value; } }

        public int LONGX { get { return longX; } set { longX = value; } }   
        public int LONGY { get { return longY; } set { longY = value; } }

        public Escenario(int x, int y, int longX, int longY, string[] bordes)
        {
            this.x = x;
            this.y = y;
            this.longX = longX;
            this.longY = longY;

            limitesSprites = bordes;
        }

        public void DibujarBordes(object locker)
        {
            for (int i = x; i <= longX; i++)
            {
                AuxMeth.PosicionarCursor(i, y, locker, limitesSprites[0]);
                AuxMeth.PosicionarCursor(i,longY, locker, limitesSprites[0]);
            }
            for (int i = y; i <= longY; i++)
            {
                AuxMeth.PosicionarCursor(x, i, locker, limitesSprites[1]);
                AuxMeth.PosicionarCursor(longX, i, locker, limitesSprites[1]);
            }
        }
    }

    class Prop
    {


    }

    class Timer
    {
        bool finalizado;

        public bool FINALIZADO { get { return finalizado; } set { finalizado = value; } }


        public Timer()
        {
            finalizado = true;
        }
    }

    class Menu
    {

        int posCursor;
        string cursor;
        int cursorBeforePos;

        public int posCURSOR
        {
            get { return posCursor; }
            set { posCursor = value; }
        }

        public string CURSOR
        {
            get { return cursor; }
            set { cursor = value; }
        }

        public Menu(string cursor) {

            this.cursor = cursor;
            posCursor = (Console.WindowTop / 2) + 5;
        }

        public void DibujarMenu()
        {
            Console.CursorLeft = (Console.BufferWidth / 2) - 5;
            Console.CursorTop = (Console.WindowTop / 2) + 5;

            Console.Write("Crear Partida.");

            Console.CursorLeft = (Console.BufferWidth / 2) - 5;
            Console.CursorTop = (Console.WindowTop / 2) + 10;

            Console.Write("Unirse a Partida.");

        }

        public void DibujarCursor()
        {

            Console.CursorLeft = (Console.BufferWidth / 2) - 9;
            Console.CursorTop = cursorBeforePos;

            

            Console.Write(String.Concat(Enumerable.Repeat(" ", CURSOR.Length)));

            Console.CursorTop = posCursor;
            Console.CursorLeft = (Console.BufferWidth / 2) - 9;
            Console.Write(cursor);
            cursorBeforePos = posCursor;

        }

    }

    class Jugador
    {
        int posX;
        int posY;
        int id;

        object locker;

        string spriteSombrero = "__▄▄__";
        string spriteCabeza =   "  ▀▀  ";
        string spriteCuerpo =  $"┌█▒▒█┐";

        int facing;

        int vida;
        bool muerto;
        bool animado;

        int ancho;
        int alto;

        List<Bala> balas;

        public int POSX { get { return posX; } set { posX = value; } }

        public int POSY { get { return posY; } set { posY = value; } }

        public int ID { get { return id; } set { id = value; } }

        public int FACING { get { return facing; } set { facing = value; } }

        public int VIDA { get { return vida; } set { vida = value; } }

        public bool MUERTO { get { return muerto; } set { muerto = value; } }   

        public bool ANIMADO { get { return animado; } set { animado = value; } }

        public List<Bala> BALAS { get { return balas; } set { balas = value; } }

        public object LOCKER { get { return locker; } set { locker = value; } } 
 
        public int ANCHO { get { return ancho; } }

        public int ALTO { get { return alto; } }


        public Jugador() {
            balas = new List<Bala>();
            this.vida = 100;
            this.muerto = false;
            this.animado = false;

        }

        public Jugador(int posX, int posY, int id, object locker, int ancho, int alto) {
            this.posX = posX ;
            this.posY = posY;
            this.id = id;
            balas = new List<Bala>(); 
            this.locker = locker;
            this.vida = 100;
            this.muerto = false;
            this.animado = false;
            this.ancho = ancho;
            this.alto = alto;
        }

        public void Dibujar()
        {
            if (muerto == false)
            {
                AuxMeth.PosicionarCursor(POSX, POSY, LOCKER, spriteSombrero);
                AuxMeth.PosicionarCursor(POSX, POSY + 1, LOCKER, spriteCabeza);
                AuxMeth.PosicionarCursor(POSX, POSY + 2, LOCKER, spriteCuerpo);
            }
            else
            {
                AuxMeth.PosicionarCursor(POSX, POSY, LOCKER, spriteSombrero, ConsoleColor.Red);
                AuxMeth.PosicionarCursor(POSX, POSY + 1, LOCKER, spriteCabeza, ConsoleColor.Red);
                AuxMeth.PosicionarCursor(POSX, POSY + 2, LOCKER, spriteCuerpo, ConsoleColor.Red);
            }
        }

        public void Borrar(int bPosX, int bPosY)
        {
            Console.CursorLeft = bPosX;
            Console.CursorTop = bPosY;                     
            AuxMeth.PosicionarCursor(bPosX, bPosY, LOCKER, "      ");

            AuxMeth.PosicionarCursor(bPosX, bPosY + 1, LOCKER, "      ");

            AuxMeth.PosicionarCursor(bPosX, bPosY + 2, LOCKER, "      ");

        }

        public async Task MorirAnimacion()
        {

            AuxMeth.PosicionarCursor(POSX, POSY, LOCKER, "         ", ConsoleColor.Red);
            AuxMeth.PosicionarCursor(POSX, POSY + 1, LOCKER, "       ", ConsoleColor.Red);
            AuxMeth.PosicionarCursor(POSX, POSY + 2, LOCKER, "       ", ConsoleColor.Red);
            AuxMeth.PosicionarCursor(POSX, POSY, LOCKER, "#_▄- *#", ConsoleColor.Red);
            AuxMeth.PosicionarCursor(POSX, POSY + 1, LOCKER, "* ▀- #*", ConsoleColor.Red);
            AuxMeth.PosicionarCursor(POSX, POSY + 2, LOCKER, "┌█▒▒█┐", ConsoleColor.Red);
            await Task.Delay(200);                       
            AuxMeth.PosicionarCursor(POSX, POSY, LOCKER, "       ", ConsoleColor.Red);
            AuxMeth.PosicionarCursor(POSX, POSY + 1, LOCKER, "       ", ConsoleColor.Red);
            AuxMeth.PosicionarCursor(POSX, POSY + 2, LOCKER, "      ", ConsoleColor.Red);
            AuxMeth.PosicionarCursor(POSX, POSY, LOCKER, "▀   ", ConsoleColor.Red);
            AuxMeth.PosicionarCursor(POSX, POSY + 1, LOCKER,"* ▀-", ConsoleColor.Red);
            AuxMeth.PosicionarCursor(POSX, POSY + 2, LOCKER, "┌█▒▀-", ConsoleColor.Red);
            await Task.Delay(200);
            AuxMeth.PosicionarCursor(POSX, POSY, LOCKER, "   ", ConsoleColor.Red);
            AuxMeth.PosicionarCursor(POSX, POSY + 1, LOCKER, "*  ▀", ConsoleColor.Red);
            AuxMeth.PosicionarCursor(POSX, POSY + 2, LOCKER, "┌█▀▀-", ConsoleColor.Red);
            await Task.Delay(200);
            AuxMeth.PosicionarCursor(POSX, POSY, LOCKER, "       ", ConsoleColor.Red);
            AuxMeth.PosicionarCursor(POSX, POSY + 1, LOCKER, "       ", ConsoleColor.Red);
            AuxMeth.PosicionarCursor(POSX, POSY + 2, LOCKER, "      ", ConsoleColor.Red);
            AuxMeth.PosicionarCursor(POSX, POSY, LOCKER, "   ", ConsoleColor.Red);
            AuxMeth.PosicionarCursor(POSX, POSY + 1, LOCKER, "   ", ConsoleColor.Red);
            AuxMeth.PosicionarCursor(POSX, POSY + 2, LOCKER, "▄▄▄▄ ██", ConsoleColor.Red);
           
        }

    }

    class Bala
    {

        int direccion;
        int sposx;
        int sposy;

        int distancia;
        //int bposx;
        //int bposy;
        int origenID;



        public Bala() { }

        public Bala(int direccion, int x, int y, int origenID)
        {
            this.direccion = direccion;
            sposx = x;
            sposy = y;
            distancia = 0;
            this.origenID = origenID;
          //  bposx = x;
          //  bposy = y;
        }


        public bool comprobarDistancia()
        {
            if (direccion == 3 || direccion == 2) {
                if (distancia > 10) { return true; }
            } else if (direccion == 1 || direccion == 0) {
                if (distancia > 30) { return true;}
            }
            return false;

        }


        public int DIRECCION { get { return direccion; } set { direccion = value; } }
        public int SPOSX { get { return sposx; } set { sposx = value; } }
        public int SPOSY { get { return sposy; } set { sposy = value; } }

        public int DISTANCIA { get { return distancia; } set { distancia = value; } }

        public int ORIGENID { get { return origenID; } set { origenID = value; } }
       // public int BPOSX { get { return bposx; } set {  bposx = value; } }
        //public int BPOSY { get { return bposy; } set { bposy = value; } }


    }
}