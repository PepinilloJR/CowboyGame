using Principal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AuxMethods
{
    public class AuxMeth
    {
        public static void PosicionarCursor(int x, int y, object locker, string dato)
        {
            lock (locker)
            {
                Console.CursorLeft = x;
                Console.CursorTop = y;
                Console.Write(dato);
            }
        }

        public static void PosicionarCursor(int x, int y, object locker, string dato, ConsoleColor color)
        {
            lock (locker)
            {
                Console.ForegroundColor = color;
                Console.CursorLeft = x;
                Console.CursorTop = y;
                Console.Write(dato);
                Console.ForegroundColor = ConsoleColor.White;
            }
        }

        public static void LeerEntrada(Entrada ent, CancellationToken token)
        {
            while (true)
            {
                ent.tecla = Console.ReadKey(intercept: true).Key.ToString();
                // Task.Delay(50);
                if (token.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }


    public class Entrada
    {
        public string tecla;

    }
}
