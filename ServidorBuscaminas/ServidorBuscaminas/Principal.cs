/***************************************************************************************
 * Programador: Renato Vargas Gómez     Fecha de Actualización:02/01/2018
 * Proyecto: Buscaminas
 * Descripción: Esta clase describe las propiedades y metodos del  servidor.
 ********************************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Web.Script.Serialization;
using Newtonsoft.Json;
using System.Data.Common;
using System.Configuration;


namespace ServidorBuscaminas
{
    class Principal
    {
        private static readonly Socket socketServidor = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        private static readonly List<Socket> socketsClientes = new List<Socket>();
        private const int MEDIDA_BUFFER = 2048;
        private const int PUERTO = 100;
        private static readonly byte[] buffer = new byte[MEDIDA_BUFFER];
        public static GeneradorLista generador = new GeneradorLista();
        public static int numeroClientes=0;
        


        static void Main()
        {
            
            Console.Title = "Servidor Buscaminas";
            EstablecerServidor();
            Console.ReadLine(); // SE CIERRA TODO CUANDO SE PRESIONA ENTER
            CerrarTodosLosSockets();
        }

        private static void EstablecerServidor()
        {
            Console.WriteLine("Estableciendo Servidor...");
            socketServidor.Bind(new IPEndPoint(IPAddress.Any, PUERTO));
            socketServidor.Listen(0);
            socketServidor.BeginAccept(AceptarCallback, null);
            Console.WriteLine("Servidor establecido");
        }

        private static void CerrarTodosLosSockets()
        {
            foreach (Socket socket in socketsClientes)
            {
                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
            }

            socketServidor.Close();
        }

        private static void AceptarCallback(IAsyncResult AR)
        {
            Socket socket;

            try
            {
                socket = socketServidor.EndAccept(AR);
            }
            catch (ObjectDisposedException) 
            {
                return;
            }

            socketsClientes.Add(socket);
            socket.BeginReceive(buffer, 0, MEDIDA_BUFFER, SocketFlags.None, RecibirCallback, socket);
            Console.WriteLine("Cliente conectado, esperando solicitud...");
            numeroClientes++;
            Console.WriteLine("Clientes Conectados= "+numeroClientes);
            socketServidor.BeginAccept(AceptarCallback, null);
        }

        private static void RecibirCallback(IAsyncResult AR)
        {
            string provider = ConfigurationManager.AppSettings["provider"];
            string connectionString = ConfigurationManager.AppSettings["connectionString"];
            DbProviderFactory factory = DbProviderFactories.GetFactory(provider);

            Socket socketActual = (Socket)AR.AsyncState;
            int recibido;

            try
            {
                recibido = socketActual.EndReceive(AR);
            }
            catch (SocketException)
            {
                Console.WriteLine("Cliente desconectado de forma forzada");
                socketActual.Close();
                socketsClientes.Remove(socketActual);
                return;
            }

            byte[] bufferRecibido = new byte[recibido];
            Array.Copy(buffer, bufferRecibido, recibido);
            string texto = Encoding.ASCII.GetString(bufferRecibido);
            Peticion nuevaPeticion = JsonConvert.DeserializeObject<Peticion>(texto);




            if (nuevaPeticion.TipoPeticion == "Comprobar usuario")
            {

                bool existe = false;
                //if (nuevaPeticion.NombreUsuario == "Renato")
                //{
                    using (DbConnection connection = factory.CreateConnection())
                    {
                        if (connection == null)
                        {
                            Console.WriteLine("Connection Error");
                            Console.ReadLine();
                            return;
                        }
                        connection.ConnectionString = connectionString;
                        connection.Open();
                        DbCommand command = factory.CreateCommand();
                        if (command == null)
                        {
                            Console.WriteLine("Command Error");
                            Console.ReadLine();
                            return;
                        }
                        command.Connection = connection;
                        command.CommandText = "Select * FROM Usuarios WHERE NombreUsuario = @NombreUsuario AND ContrasenaUsuario=@ContrasenaUsuario";
                        DbParameter paramNombreUsuario = command.CreateParameter();
                        paramNombreUsuario.ParameterName = "@NombreUsuario";
                        paramNombreUsuario.Value = nuevaPeticion.NombreUsuario;
                        command.Parameters.Add(paramNombreUsuario);

                        DbParameter paramContrasenaUsuario = command.CreateParameter();
                        paramContrasenaUsuario.ParameterName = "@ContrasenaUsuario";
                        paramContrasenaUsuario.Value = nuevaPeticion.Contrasena;
                        command.Parameters.Add(paramContrasenaUsuario);




                    using (DbDataReader dataReader = command.ExecuteReader())
                        {
                            while (dataReader.Read())
                            {
                                Console.WriteLine($" {dataReader["Victorias"]}");
                                existe = true;
                            }
                        }
                        connection.Close();
                        //Console.ReadLine();

                    }
                //}

                if (existe == true)
                {
                    byte[] data = Encoding.ASCII.GetBytes("El usuario existe");
                    socketActual.Send(data);
                }
                else
                {
                    byte[] data = Encoding.ASCII.GetBytes("El usuario NO existe");
                    socketActual.Send(data);
                }

            }

            if (nuevaPeticion.TipoPeticion == "Enviar Lista")
            {
                Console.WriteLine("Se enviara la lista");
                Console.WriteLine(generador.getNombre());
                GeneradorListaEstatico generadorEstatico = new GeneradorListaEstatico
                {
                    PosicionesMinasX = generador.GetListaX(),
                    PosicionesMinasY = generador.GetListaY(),
                    Nombre = generador.getNombre(),


                };
                string json = JsonConvert.SerializeObject(generadorEstatico);
                int[] posicionesX = new int[8];
                int[] posicionesY = new int[8];
                posicionesX = generador.GetListaX();
                posicionesY = generador.GetListaY();
                Console.WriteLine("lista X");
                for (int i = 0; i < 8; i++)
                {
                    Console.WriteLine(posicionesX[i]);
                }
                Console.WriteLine("lista Y");
                for (int i = 0; i < 8; i++)
                {
                    Console.WriteLine(posicionesY[i]);
                }

                string jsonString = json;
                string listasStringJson = json;
                Console.WriteLine(listasStringJson);
                Console.WriteLine(json);
                byte[] bufferString = Encoding.ASCII.GetBytes(json);
                socketActual.Send(bufferString);
            }

            if (nuevaPeticion.TipoPeticion == "get time") // Client requested time
            {
                Console.WriteLine("Text is a get time request");
                byte[] data = Encoding.ASCII.GetBytes("La hora");
                socketActual.Send(data);
                Console.WriteLine("Time sent to client");
            }

            socketActual.BeginReceive(buffer, 0, MEDIDA_BUFFER, SocketFlags.None, RecibirCallback, socketActual);
        }
    }
}
