using AsyncSocketServer;
using MasterLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AsyncSocketServerTest
{
    class Program
    {
        private static AsyncSocketListener asl;
        protected static Logger _logger;

        static void Main(string[] args)
        {
            string logPath = Directory.GetCurrentDirectory();
            _logger = new Logger(logPath, "testLog");
            asl = new AsyncSocketListener(61081, _logger);
            Console.WriteLine(asl.Ping("192.168.1.139"));
            Console.ReadKey();
        }
    }
}
