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
        public static string jsonGenerador;
        public static int numeroClientes=0;
        public static int numeroJugadores = 0;
        


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
            foreach (Socket socket in socketsClientes)//////////////////////////
            {

                socket.Shutdown(SocketShutdown.Both);///////////////////////////////////
                socket.Close();/////////////////////////////////////////
            }////////////////////////////////////

            socketServidor.Close();
        }
        private static void BroadcastVictoria() {
            byte[] mensaje = Encoding.ASCII.GetBytes("Partida Terminada");
            foreach (Socket socket in socketsClientes) {
                socket.Send(mensaje);
            }
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
            Console.WriteLine((Socket)AR.AsyncState);
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
                        Console.WriteLine("usuario comprobado");




                    using (DbDataReader dataReader = command.ExecuteReader())
                        {
                            while (dataReader.Read())
                            {
                                //Console.WriteLine($" {dataReader["Victorias"]}");
                                existe = true;
                                numeroJugadores++;
                                Console.WriteLine("Numero de Jugadores: "+numeroJugadores);
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

            if (nuevaPeticion.TipoPeticion == "Ranking") { 
                string[] rankingArreglo = new string [15];
                TablaClasificacion tabla = new TablaClasificacion();
                Console.WriteLine("Peticion de Ranking Recibida");
                int i = 0;
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
                    command.CommandText = "Select * FROM Perfiles ORDER BY NumeroVictorias DESC";

                    using (DbDataReader dataReader = command.ExecuteReader())
                    {
                        int contador=0;
                        while (dataReader.Read())
                        {
                            Console.WriteLine(i);
                            if (i < 5) {
                               rankingArreglo[contador] = $" {dataReader["NombreUsuario"]}";
                                contador++;
                                rankingArreglo[contador] = $" {dataReader["NumeroVictorias"]}";
                                contador++;
                                rankingArreglo[contador] = $" {dataReader["Partidas"]}";
                                contador++;
                                i = i + 1;
                            }
                            
                            Console.WriteLine($" {dataReader["NombreUsuario"]}"
                                + $" {dataReader["NumeroVictorias"]}"
                                + $" {dataReader["Partidas"]}");
                        }
                    }
                    tabla.ranking = rankingArreglo;
                    string jsonRanking = JsonConvert.SerializeObject(tabla);
                    Console.WriteLine(jsonRanking );
                    connection.Close();
                    //Console.ReadLine();
                    byte[] bufferString = Encoding.ASCII.GetBytes(jsonRanking);
                    socketActual.Send(bufferString);

                }

            }

            if (nuevaPeticion.TipoPeticion == "Registrar Usuario")
            {
                bool existe = false;
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
                    command.CommandText = "Select * FROM Usuarios WHERE NombreUsuario = @NombreUsuario";
                    DbParameter paramNombreUsuario = command.CreateParameter();
                    paramNombreUsuario.ParameterName = "@NombreUsuario";
                    paramNombreUsuario.Value = nuevaPeticion.NombreUsuario;
                    command.Parameters.Add(paramNombreUsuario);

                    using (DbDataReader dataReader = command.ExecuteReader())
                    {
                        while (dataReader.Read())
                        {
                            Console.WriteLine($" {dataReader["NombreUsuario"]}");
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

            //////////////////////////////////////////////////////////////////
            if (nuevaPeticion.TipoPeticion == "Enviar Lista")
            {
                if (numeroJugadores == 2)
                {
                    byte[] bufferGenerador = Encoding.ASCII.GetBytes(jsonGenerador);
                    socketActual.Send(bufferGenerador);
                }
                else if(numeroJugadores==1){
                    Console.WriteLine("Se enviara la lista");
                    Console.WriteLine(generador.getNombre());
                    GeneradorListaEstatico generadorEstatico = new GeneradorListaEstatico
                    {
                        PosicionesMinasX = generador.GetListaX(),
                        PosicionesMinasY = generador.GetListaY(),
                        Nombre = generador.getNombre(),


                    };
                    string json = JsonConvert.SerializeObject(generadorEstatico);
                    jsonGenerador = json;
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
                
            }


            if (nuevaPeticion.TipoPeticion == "Nuevo Registro") {
                Console.WriteLine("Se recibió la peticionde nuevo registro");
                string mensaje="usuario no creado";
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
                    Console.WriteLine("Se realizara la consulta");
                    command.CommandText = "INSERT INTO Usuarios VALUES (@NombreUsuario, @ContrasenaUsuario)";
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

                            mensaje = "Usuario Creado";
                        }
                    }
                    Console.WriteLine("Se realizo la consulta");
                    DbCommand commando = factory.CreateCommand();
                    if (commando == null)
                    {
                        Console.WriteLine("Command Error");
                        Console.ReadLine();
                        return;
                    }
                    commando.Connection = connection;
                    Console.WriteLine("Se realizara la consulta");
                    commando.CommandText = "INSERT INTO PERFILES (NombreUsuario) VALUES (@NombreUsuario)";
                    DbParameter paramNombreUsuario2 = commando.CreateParameter();
                    paramNombreUsuario2.ParameterName = "@NombreUsuario";
                    paramNombreUsuario2.Value = nuevaPeticion.NombreUsuario;
                    commando.Parameters.Add(paramNombreUsuario2);


                    using (DbDataReader dataReader = commando.ExecuteReader())
                    {
                        while (dataReader.Read())
                        {

                            mensaje = "Usuario Creado 2";
                        }
                    }
                    connection.Close();
                    //Console.ReadLine();

                }
                byte[] data = Encoding.ASCII.GetBytes("Usuario Creado");
                socketActual.Send(data);

            }


            if (nuevaPeticion.TipoPeticion == "banderas capturadas") // Cliente da a concer que capturó todas la banderas
            {
                Console.WriteLine("Un cliente capturo todas las banderas");
                BroadcastVictoria();
                Console.WriteLine("Mensaje enviado a todos los clientes");
            }

            if (nuevaPeticion.TipoPeticion == "numeroJugadores") {
                string numeroJugadoresString = "";
                if (numeroJugadores == 1)
                {
                    numeroJugadoresString = "1";
                } else if (numeroJugadores==2) {
                    numeroJugadoresString = "2";
                }
                byte[] data = Encoding.ASCII.GetBytes(numeroJugadoresString);
                socketActual.Send(data);

            }
            if (nuevaPeticion.TipoPeticion == "ReducirNumeroJugadores") {
                numeroJugadores=numeroJugadores-1;
            }

            socketActual.BeginReceive(buffer, 0, MEDIDA_BUFFER, SocketFlags.None, RecibirCallback, socketActual);
        }
    }
}
