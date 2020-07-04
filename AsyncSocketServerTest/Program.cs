using AsyncSocketServer;
using MasterLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AsyncSocketServerTest
{
    class Program
    {
        private static AsyncSocketListener asl;
        private static Logger _logger;

        static void Main(string[] args)
        {
            AsyncSocketThread scktThrd;
            Thread thSocket;
            string logPath = Directory.GetCurrentDirectory();
            _logger = new Logger(logPath, "testLog");
            asl = new AsyncSocketListener(61081, _logger, 10);
            asl.DataFromSocket += Asl_DataFromSocket;
            asl.ErrorFromSocket += Asl_ErrorFromSocket;
            foreach (IPAddress IP in asl.SrvIpAddress)
            {
                Console.WriteLine(asl.Ping(IP));
                _logger.Log(LogLevel.INFO, string.Format("Address found {0}", IP));
            }
                
            asl.Echo = false;

            scktThrd = new AsyncSocketThread();
            scktThrd.Log = _logger;
            scktThrd.AsyncSocketListener = asl;
            scktThrd.Interval = 100;
            thSocket = new Thread(scktThrd.AsyncSocket);
            
            thSocket.Start();

            Console.ReadKey();
        }

        private static void Asl_ErrorFromSocket(object sender, string e)
        {
            Socket Handler = sender as Socket;
        }

        private static void Asl_DataFromSocket(object sender, SocketManagerInfo.SocketMessageStructure e)
        {
            Socket Handler = sender as Socket;
            asl.Send(Handler, string.Format("{0};{1};{2}", e.Command, e.Data, e.SendingTime));
        }
    }
}
