using NModbus;
using System;
using System.Net;
using System.Net.Sockets;

namespace UETM2;

public class ConnectionManager
{
    private static Tuple<TcpClient, IModbusMaster>? _connection;

    public static Tuple<TcpClient, IModbusMaster> OpenConnection(string ipAddress, int modBusPort)
    {
        if (_connection != null)
            throw new InvalidOperationException("Соединение уже открыто.");

        if (!IPAddress.TryParse(ipAddress, out _))
            throw new ArgumentException("Некорректный IP-адрес.");

        if (modBusPort < 1 || modBusPort > 65535)
            throw new ArgumentOutOfRangeException(nameof(modBusPort), "Порт должен быть в диапазоне от 1 до 65535.");

        TcpClient tcpClient = new TcpClient();
        try
        {
            IAsyncResult result = tcpClient.BeginConnect(ipAddress, modBusPort, null, null);
            bool success = result.AsyncWaitHandle.WaitOne(2000);
            if (!success)
            {
                tcpClient.Close();
                throw new TimeoutException("Не удалось подключиться к устройству.");
            }
            tcpClient.EndConnect(result);

            tcpClient.ReceiveTimeout = 2000;
            tcpClient.SendTimeout = 2000;

            var factory = new ModbusFactory();
            IModbusMaster modbusMaster = factory.CreateMaster(tcpClient);

            _connection = new Tuple<TcpClient, IModbusMaster>(tcpClient, modbusMaster);
            return _connection;
        }
        catch (Exception)
        {
            tcpClient.Close();
            throw;
        }
    }

    public static void CloseConnection()
    {
        if (_connection != null)
        {
            try
            {
                if (_connection.Item1?.Client != null)
                    _connection.Item1.Client.Shutdown(SocketShutdown.Both);
            }
            catch { }
            try
            {
                _connection.Item1?.Close();
            }
            catch { }
            _connection = null;
        }
    }
}