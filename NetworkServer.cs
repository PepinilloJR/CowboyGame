using Principal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace juegoOnlineBasico
{
    class Server
    {
        IPAddress iP;

        IPEndPoint iPEndPoint;

        Socket socket ;

        List<Socket> clientes = new List<Socket>();

        Dictionary<int,Jugador> datosJugadores = new Dictionary<int, Jugador>();   

        public Server(string ip, int port)
        {
            this.iP = IPAddress.Parse(ip);

            iPEndPoint = new IPEndPoint(this.iP, port);


        }

        public void Iniciar()
        {
            socket = new Socket(this.iP.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            socket.Bind(iPEndPoint);

            socket.Listen(30);

            LogicaServer();
        }

        public async void LogicaServer()
        {

            Task.Run(Sender);
            
            while (true)
            {
                Socket cliente = socket.Accept();
                clientes.Add(cliente);
                Console.WriteLine("Conexion aceptada");

                Task.Run(() => { Receiver(cliente); });

            }

        }


        async Task Receiver(Socket cliente)
        {
            while (true)
            {
                byte[] buffer = new byte[2048];

                await cliente.ReceiveAsync(buffer);

                string mensaje = Encoding.ASCII.GetString(buffer);
                string json = mensaje.TrimEnd('\0');
                try
                {
                   

                    Jugador jugador = JsonSerializer.Deserialize<Jugador>(json);

                // intenta lockear esta parte de abajo, en una funcion encargada de modificar ese dato, podria ser ese el problema

                    if (datosJugadores.ContainsKey(jugador.ID))
                    {
                        datosJugadores[jugador.ID] = jugador;
                       // Console.WriteLine(jugador.ID);
                    }
                    else
                    {
                        datosJugadores.Add(jugador.ID, jugador);
                       // Console.WriteLine(jugador.ID);
                    }

                    //Console.WriteLine("jugador actualizado");
                    //Console.WriteLine(json);

                }
                catch (Exception ex)
                {
                    Console.WriteLine("al recibir: " + ex.ToString());
                    Console.WriteLine(json);
                    continue;
                }
            }

        }

        async Task Sender()
        {
            while (true)
            {
                await Task.Delay(50);
                foreach (Socket cliente in clientes.ToArray())
                {
                    try
                    {
                        Dictionary<int, Jugador> datosParaPasar = datosJugadores.ToDictionary();
                        string json = JsonSerializer.Serialize(datosParaPasar);
                    
                        await cliente.SendAsync(Encoding.ASCII.GetBytes(json));
                        //Console.WriteLine($"datos enviados a {cliente}");
                    } catch (Exception ex)
                    {
                        Console.WriteLine("al enviar: " + ex.ToString());
                    }
                    
                }

            }
        }


        async Task CloseServer()
        {
            socket.Shutdown(SocketShutdown.Both);
            socket.Close();

            foreach (Socket cliente in clientes.ToArray()) { 
                cliente.Shutdown(SocketShutdown.Both);
                cliente.Close(); 
            }
        }
    }

    class Mensaje
    {

    }



}



