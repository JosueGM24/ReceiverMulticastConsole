using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MulticastConsoleApp
{
    class Program
    {
        private static UdpClient udpClient;
        private static Task listenTask;
        private static CancellationTokenSource cancellationTokenSource;

        static async Task Main(string[] args)
        {
            Console.WriteLine("Introduce el grupo multicast:");
            string multicastGroup = Console.ReadLine();

            Console.WriteLine("Introduce el puerto multicast:");
            if (!int.TryParse(Console.ReadLine(), out int multicastPort))
            {
                Console.WriteLine("Escriba un grupo y puerto correctos");
                return;
            }

            Console.WriteLine("Introduce la URL (deja en blanco si no aplica):");
            string apiUrl = Console.ReadLine();

            cancellationTokenSource = new CancellationTokenSource();
            listenTask = ListenMulticast(multicastGroup, multicastPort, apiUrl, cancellationTokenSource.Token);

            Console.WriteLine("Presione Enter para detener...");
            Console.ReadLine();

            cancellationTokenSource.Cancel();
            udpClient?.Close();
            await listenTask;
        }

        private static async Task ListenMulticast(string multicastGroup, int multicastPort, string apiUrl, CancellationToken cancellationToken)
        {
            try
            {
                udpClient = new UdpClient();
                udpClient.ExclusiveAddressUse = false;
                udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, multicastPort));
                udpClient.JoinMulticastGroup(IPAddress.Parse(multicastGroup));

                Console.WriteLine($"Unido al grupo multicast {multicastGroup} en el puerto {multicastPort}");

                while (!cancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine("Esperando datos...");
                    var receiveTask = udpClient.ReceiveAsync();
                    var completedTask = await Task.WhenAny(receiveTask, Task.Delay(Timeout.Infinite, cancellationToken));

                    if (completedTask == receiveTask)
                    {
                        var result = await receiveTask;
                        string operacion = Encoding.UTF8.GetString(result.Buffer).Trim();

                        if (string.IsNullOrEmpty(apiUrl))
                        {
                            Console.WriteLine("Datos recibidos");
                            Console.WriteLine("Operación recibida: " + operacion);
                        }
                        else
                        {
                            await ProcessWithApi(operacion, apiUrl);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Conexión cancelada.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error en el socket multicast: " + ex.Message);
            }
            finally
            {
                if (udpClient != null)
                {
                    try
                    {
                        udpClient.DropMulticastGroup(IPAddress.Parse(multicastGroup));
                        udpClient.Close();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error al cerrar el socket multicast: " + ex.Message);
                    }
                    finally
                    {
                        udpClient = null;
                    }
                }
            }
        }

        private static async Task ProcessWithApi(string operacion, string apiUrl)
        {
            try
            {
                operacion = WebUtility.UrlEncode(operacion);
                string fullUrl = $"{apiUrl}{operacion}";
                Console.WriteLine($"Procesando en {fullUrl}");

                using (var httpClient = new HttpClient())
                {
                    var response = await httpClient.GetAsync(fullUrl);

                    if (response.IsSuccessStatusCode)
                    {
                        string content = await response.Content.ReadAsStringAsync();
                        Console.WriteLine("Result: " + content);
                    }
                    else
                    {
                        Console.WriteLine("Error en la respuesta del servidor: " + response.ReasonPhrase);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error en la conexión HTTP: " + ex.Message);
            }
        }
    }
}
