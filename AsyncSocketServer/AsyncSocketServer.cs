//05.07.2020
//14.07.2020
//29.04.2021 così visualizzo il nome corretto del metodo
//15.06.21 il buffer è stato potato da 180000 byte a 1800000
//15.06.2021  corretto gestione callback ricezione da socket. questo evita il warning di comando non riconosciuto.
//10.02.2022 Chiude i socket nell'intervallo previsto
using MasterLog;
using MessaggiErrore;
using SocketManagerInfo;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace AsyncSocketServer
{
    public class ErrorPortException : Exception
    {
        public ErrorPortException() : base() { }
        public ErrorPortException(string message) : base(message){ }
        public ErrorPortException(string message, System.Exception inner): base(message) { }
    }
    public class StateObject
    {
        // Client  socket.  
        public Socket workSocket = null;
        // Size of receive buffer.  
        public static int locBufferSize = 1800000; // 15.06.21 il buffer è stato potato da 180000 byte a 1800000
        // Receive buffer.  
        public byte[] buffer;
        // Received data string.  
        public StringBuilder sb = new StringBuilder();
        public StateObject()
        {
            buffer = new byte[locBufferSize];
            
        }
        public StateObject(int BufferSize)
        {
            buffer = new byte[BufferSize];
            locBufferSize = buffer.Count();
        }
        public static int BufferSize { get { return locBufferSize; } }
    }
    public class AsyncSocketListener
    {
        private const string logPrefisso = "<SOCKET>";
        private List<IPAddress> ipAddressLocal = new List<IPAddress>();
        private string ipAddressCaller;
        private int port = 0;
        // private short indexIP = 0;
        private Logger _log;
        public ManualResetEvent allDone = new ManualResetEvent(false);
        private Socket listener;
        private Socket handler;
        /// <summary>
        /// This event sends data received via sockets to the class instance
        /// </summary>
        public event EventHandler<SocketMessageStructure> DataFromSocket;
        /// <summary>
        /// When an error occurs in the socket server this event returns the message contained in the exception
        /// </summary>
        public event EventHandler<string> ErrorFromSocket;
        #region "* * *  Class constructor  * * * "
        public AsyncSocketListener()
        {
            
        }
        public AsyncSocketListener(UInt16 Port, short PortRange = 0)
        {
            MethodBase thisMethod = MethodBase.GetCurrentMethod();
            try
            {
                _log = null;
                if (PortRange > 0)
                {
                    this.port = LookingforFreePort(Port, (UInt16)(Port + PortRange)); ;
                }
                else
                {
                    this.port = LookingforFreePort(Port, Port);
                }
            }
            catch (Exception ex)
            {
                string msg = string.Format("{0} {1}", logPrefisso, ClsMessaggiErrore.CustomMsg(ex, thisMethod));
                throw new Exception(msg, ex);
            }
        }
        public AsyncSocketListener(UInt16 Port, Logger ServiceLog, short PortRange = 0 )
        {
            MethodBase thisMethod = MethodBase.GetCurrentMethod();
            try
            {
                _log = ServiceLog;
                if (PortRange > 0)
                {
                    this.port = LookingforFreePort(Port, (UInt16)(Port + PortRange));
                }
                else
                {
                    this.port = LookingforFreePort(Port, Port);
                }
                ReadLocalAddressIP();
                //if ((this.SwDebug & _log.LOG_INFO) == _log.LOG_INFO)
                if (_log != null) _log.Log(LogLevel.INFO, string.Format("{0}(2) Port:{1} ", logPrefisso, this.port));
            }
            catch (Exception ex)
            {
                string msg = string.Format("{0} {1}", logPrefisso, ClsMessaggiErrore.CustomMsg(ex, thisMethod));
                if (_log != null) _log.Log(LogLevel.ERROR, msg); else throw new Exception(msg, ex);
            }
        }
        public AsyncSocketListener(string Port, Logger ServiceLog)
        {
            MethodBase thisMethod = MethodBase.GetCurrentMethod();
            int tryPort = 0;
            try
            {
                string[] sPort = Port.Split(';');
                _log = ServiceLog;
                // it tries 3 times to assign an available socket port, failing the attempts an error occurs
                while (this.port == 0)
                {
                    foreach (string s in sPort)
                    {
                        if (isPortAvailable(Convert.ToInt32(s)))
                        {
                            this.port = Convert.ToInt32(s);
                            break;
                        }
                        Thread.Sleep(1000);
                    }
                    if (tryPort > 2) throw new ErrorPortException("no socket ports found");
                    tryPort++;
                }
                // _log.Log(LogLevel.INFO, string.Format("{0} Method: {1} Port:{2} before ReadLocalAddressIP", thisMethod.Name, logPrefisso, this.port));

                ReadLocalAddressIP();
                //                  if ((this.SwDebug & _log.LOG_INFO) == _log.LOG_INFO)
                if (_log != null) _log.Log(LogLevel.INFO, string.Format("{0} Method: {1} Port:{2} after ReadLocalAddressIP", logPrefisso, thisMethod.ReflectedType.Name, this.port)); //29.04.2021 così visualizzo il nome corretto del metodo
            }
            catch (ErrorPortException epx)
            {
                string msg = string.Format("{0} {1}", logPrefisso, ClsMessaggiErrore.CustomMsg(epx, thisMethod));
                throw new Exception(msg);
            }
            catch (Exception ex)
            {
                string msg = string.Format("{0} {1}", logPrefisso, ClsMessaggiErrore.CustomMsg(ex, thisMethod));
                if (_log != null) _log.Log(LogLevel.ERROR, msg); else throw new Exception(msg, ex);
            }
        }
        #endregion"          * * * That's all folks  * * *           "
        #region "* * *  PUBLIC METHODS  * * *"
        public void Listening(int Timeout)
        {
            MethodBase thisMethod = MethodBase.GetCurrentMethod();
            try
            {
                // Set the event to nonsignaled state.  
                allDone.Reset();

                // Start an asynchronous socket to listen for connections.  
                if (_log != null)
                    //if ((this.SwDebug & _log.LOG_INFO) == _log.LOG_INFO)
                    _log.Log(LogLevel.INFO, logPrefisso + "(6) Waiting for a connection...");
                else
                    Console.WriteLine("Waiting for a connection...");
                listener.BeginAccept(new AsyncCallback(AcceptCallback), listener);

                // Wait until a connection is made before continuing.  
                allDone.WaitOne(Timeout); // 16.06.2020 Provo a mettere un timeout? MA BISOGNA CAPIRE SE FA CASINO? 
            }catch(Exception ex)
            {
                string msg = string.Format("{0} {1} {2}", logPrefisso, ClsMessaggiErrore.CustomMsg(ex, thisMethod), ((SocketException)ex).ErrorCode );
                if (_log != null) _log.Log(LogLevel.ERROR, msg); else throw new Exception(msg, ex);
                ErrorFromSocket?.Invoke(handler, ex.Message);
            }
        }
        public void Listening()
        {
            MethodBase thisMethod = MethodBase.GetCurrentMethod();
            try
            {
                // Set the event to nonsignaled state.  
                allDone.Reset();

                // Start an asynchronous socket to listen for connections.  
                if (_log != null)
                    //if ((this.SwDebug & _log.LOG_INFO) == _log.LOG_INFO)
                    _log.Log(LogLevel.INFO, logPrefisso + "(5)" + "Waiting for a connection...");
                else Console.WriteLine("Waiting for a connection...");
                listener.BeginAccept(new AsyncCallback(AcceptCallback), listener);
            }catch(Exception ex)
            {
                string msg = string.Format("{0} {1} {2}", logPrefisso, ClsMessaggiErrore.CustomMsg(ex, thisMethod), ((SocketException)ex).ErrorCode);
                if (_log != null) _log.Log(LogLevel.ERROR, msg); else throw new Exception(msg, ex);
                ErrorFromSocket?.Invoke(handler, ex.Message);
            }
            finally
            {
                // Wait until a connection is made before continuing.  
                allDone.WaitOne();
            }
        }
        /// <summary>
        /// 10.02.2022 Chiude i socket nell'intervallo previsto
        /// </summary>
        /// <param name="TimeExpire">Tempo di scadenza espresso in secondi</param>
        public void CloseServerSocket(int TimeExpire)
        {
            MethodBase thisMethod = MethodBase.GetCurrentMethod();
            int Pos = 0;
            try
            {
                Pos = 1;
                this.handler.Disconnect(false);
                Pos = 2;
                // this.listener.Disconnect(false);
                if (TimeExpire != 0)
                {
                    Pos = 3;
                    if (_log != null)
                        _log.Log(LogLevel.INFO, string.Format("Server socket expire in {0}sec", TimeExpire.ToString()));
                    //this.listener.Close(TimeExpire);
                    Pos = 4;
                    this.handler.Close(TimeExpire);
                    // Pos = 5;
                    //this.handler.Shutdown(SocketShutdown.Both);
                }
                else
                {
                    Pos = 5;
                    if (_log != null)
                        _log.Log(LogLevel.INFO, "Expiration time not set");
                    //this.listener.Close();
                    Pos = 6;
                    this.handler.Close();
                }
            }
            catch (Exception ex)
            {
                string msg = string.Format("{0} {1} Pos:{2}", logPrefisso, ClsMessaggiErrore.CustomMsg(ex, thisMethod), Pos);
                if (_log != null)
                    _log.Log(LogLevel.ERROR, msg);

                throw new Exception(msg);
            }
        }
        /// <summary>
        /// Ricerca una porta disponibile nel range proposto
        /// </summary>
        /// <param name="PortInit">Porta inizio ricerca</param>
        /// <param name="PortEnd">Porta fine ricerca</param>
        /// <returns></returns>
        public UInt16 LookingforFreePort(UInt16 PortInit, UInt16 PortEnd)
        {
            MethodBase thisMethod = MethodBase.GetCurrentMethod();
            int lPort = 0;
            try
            {
                lPort = PortInit;
                while (!isPortAvailable(lPort) || lPort > PortEnd)
                {
                    lPort++;
                }
            }
            catch (Exception ex)
            {
                string msg = string.Format("{0} {1}", logPrefisso, ClsMessaggiErrore.CustomMsg(ex, thisMethod));
                //if (_log != null) _log.Log(LogLevel.ERROR, msg); else throw new Exception(msg, ex);
                throw new Exception(msg);
            }
            return (UInt16)(lPort > PortEnd ? -1 : lPort);
        }
        /// <summary>
        /// Determina lo stato del socket
        /// </summary>
        /// <param name="Soc">socket da interrogare </param>
        /// <returns>"se torna false il socket non è disponibile altrimenti true"</returns>
        public bool SocketConnected(Socket Soc)
        {
            bool check1= Soc.Poll(1000, SelectMode.SelectRead);
            bool check2 = (Soc.Available == 0);
            return !(check1 && check2);
        }
        /// <summary>
        /// Querying an IP address
        /// </summary>
        /// <param name="Address">Ip address to query (string) </param>
        /// <returns></returns>
        public string Ping(string Address)
        {
            Ping pingSender = new Ping();
            PingOptions options = new PingOptions();
            string data = "";
            string sRet = "";
            options.DontFragment = true;
            byte[] buffer = Encoding.ASCII.GetBytes(data.PadLeft(32, 'a'));
            int timeout = 120;
            PingReply reply = pingSender.Send(Address,timeout,buffer);
            if (reply.Status == IPStatus.Success)
                sRet = string.Format("Address: {0} Time (milli sec.): {1} Buffer size: {2}", reply.Address.ToString(), reply.RoundtripTime, reply.Buffer.Length);
            else
                sRet = string.Format("Address {0} not found", Address);
            return sRet;
        }
        /// <summary>
        /// Querying an IP address
        /// </summary>
        /// <param name="Address">Ip address to query (IPAddress) </param>
        /// <returns></returns>
        public string Ping(IPAddress Address)
        {
            Ping pingSender = new Ping();
            PingOptions options = new PingOptions();
            string data = "";
            string sRet = "";
            options.DontFragment = true;
            byte[] buffer = Encoding.ASCII.GetBytes(data.PadLeft(32, 'a'));
            int timeout = 120;
            PingReply reply = pingSender.Send(Address, timeout, buffer);
            if (reply.Status == IPStatus.Success)
                sRet = string.Format("Address: {0} Time (milli sec.): {1} Buffer size: {2}", reply.Address.ToString(), reply.RoundtripTime, reply.Buffer.Length);
            else
                sRet = string.Format("Address {0} not found", Address);
            return sRet;
        }

        public void Send(Socket handler, String data)
        {
            MethodBase thisMethod = MethodBase.GetCurrentMethod();
            try
            {
                byte[] byteData = Encoding.ASCII.GetBytes(data);
                // Begin sending the data to the remote device.  
                handler.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(SendCallback), handler);
            }
            catch (Exception ex)
            {
                string msg = string.Format("{0} {1}", logPrefisso, ClsMessaggiErrore.CustomMsg(ex, thisMethod));
                if (_log != null) _log.Log(LogLevel.ERROR, msg); else throw new Exception(msg, ex);
                // throw new Exception(msg, ex);
            }
        }
        public void StartListening(Logger _log = null)
        {
            MethodBase thisMethod = MethodBase.GetCurrentMethod();
            IPAddress ipAddress = null;
            //IPAddress[] ipv4Addresses = null;
            try
            {
                //if (_log != null) _log.Log(LogLevel.INFO, logPrefisso + "Acquisition of the IP address");
                //ipv4Addresses = Array.FindAll(Dns.GetHostEntry(string.Empty).AddressList, a => a.AddressFamily == AddressFamily.InterNetwork);
                //if (ipv4Addresses.Count() == 0)
                //    throw new Exception("No listening server socket was found");
                //else if (_log != null)
                //    foreach (IPAddress ip in ipv4Addresses)
                //    {
                //        ipAddressLocal.Add(ip);
                //        _log.Log(LogLevel.INFO, string.Format("{0}IP address found: {1} ", logPrefisso, ip));

                //    }
                ipAddress = System.Net.IPAddress.Any; // <- questo consente di far partire il server socket con l'indirizzo 0.0.0.0
                IPEndPoint localEndPoint = new IPEndPoint(ipAddress, this.port);
                listener = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    listener.Bind(localEndPoint);
                    listener.Listen(100);
                }
                catch (Exception ex)
                {
                    string msg = string.Format("{0} {1}", logPrefisso, ClsMessaggiErrore.CustomMsg(ex, thisMethod));
#if DEBUG
                    Console.WriteLine(msg);
#endif
                    if (_log != null)
                        _log.Log(LogLevel.ERROR, msg); else throw new Exception(msg, ex);
                }

            }catch(Exception ex)
            {
                string msg = string.Format("{0} {1} {2}", logPrefisso, ClsMessaggiErrore.CustomMsg(ex, thisMethod), ((SocketException)ex).ErrorCode);
#if DEBUG
                Console.WriteLine(msg);
#endif
                if (_log != null)
                    _log.Log(LogLevel.ERROR, msg); else throw new Exception(msg, ex);
            }
        }
        #endregion"          * * * That's all folks  * * *           "
        #region "* * *  PRIVATE METHODS  * * *"
        private void AcceptCallback(IAsyncResult ar)
        {
            MethodBase thisMethod = MethodBase.GetCurrentMethod();
            try
            {
                // Signal the main thread to continue.  
                allDone.Set();
                // Get the socket that handles the client request.  
                Socket listener = (Socket)ar.AsyncState;
                Socket handler = listener.EndAccept(ar);

                // Create the state object.  
                StateObject state = new StateObject();
                state.workSocket = handler;
                handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadCallback), state);

            }
            catch (Exception ex)
            {
                string msg = string.Format("{0} {1}", logPrefisso, ClsMessaggiErrore.CustomMsg(ex, thisMethod));
                throw new Exception(msg);
            }
        }
        private SocketMessageStructure deserializedMessage(string content)
        {
            SocketMessageStructure Sms = null;
            try
            {
                XDocument xd = XDocument.Parse(content);
                var n = from N in xd.Elements() where N.Name.LocalName == "SocketMessageStructure" select N;
                if (n.Any())
                {
                    XmlSerializer xmls = new XmlSerializer(typeof(SocketMessageStructure));
                    using (TextReader textReader = new StringReader(content))
                    {
                        //XmlReader myXmlReader = XmlReader.Create(textReader);
                        //if (xmls.CanDeserialize(myXmlReader))
                        {
                            Sms = (SocketMessageStructure)xmls.Deserialize(textReader);
                        }
                    }
                }
            }
            catch
            {
                throw;
            }
            return Sms;
        }
        private bool isPortAvailable(int port)
        {
            MethodBase thisMethod = MethodBase.GetCurrentMethod();
            IPGlobalProperties ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
            TcpConnectionInformation[] tcpConnInfoArray = ipGlobalProperties.GetActiveTcpConnections();
            using (StreamWriter wr = new StreamWriter(Path.Combine(Environment.CurrentDirectory, "Addreslist.txt"), false))
            {
                foreach (var v in tcpConnInfoArray)
                {
                    wr.WriteLine("ADDRESS: {0} PORT: {1} STATE: {2}", v.LocalEndPoint.Address, v.LocalEndPoint.Port, v.State);
                }
            }
            bool ret = false;
            try
            {
                var V = tcpConnInfoArray.Where(TCP => TCP.LocalEndPoint.Port == port);
                if (V.Any())
                {
                    if (_log != null) 
                    _log.Log(LogLevel.WARNING, string.Format("{0} ADDRESS: {1} PORT: {2} STATE: {3}", logPrefisso, V.First().LocalEndPoint.Address, V.First().LocalEndPoint.Port, V.First().State));
                }else
                    ret = !V.Any();
            }
            catch(Exception ex)
            {
                string msg = string.Format("{0} {1} {2}", logPrefisso, ClsMessaggiErrore.CustomMsg(ex, thisMethod), ((SocketException)ex).ErrorCode);
                //if (_log != null) _log.Log(LogLevel.ERROR, msg); else throw new Exception(msg, ex);
                throw new Exception(msg);
            }
            return ret;
        }
        private void ReadCallback(IAsyncResult ar)
        {
            MethodBase thisMethod = MethodBase.GetCurrentMethod();
            string content = string.Empty;
            // Retrieve the state object and the handler socket  
            // from the asynchronous state object.  
            StateObject state = (StateObject)ar.AsyncState;
            if (_log != null)
                //if ((this.SwDebug & _log.LOG_INFO) == _log.LOG_INFO)
                _log.Log(LogLevel.INFO, string.Format("{0} DAta from ipHost:port= {1}", logPrefisso, ((AsyncSocketServer.StateObject)ar.AsyncState).workSocket.RemoteEndPoint));
            // This address must be remembered, because it is the one where to send the biometric data
            string[] caller = ((AsyncSocketServer.StateObject)ar.AsyncState).workSocket.RemoteEndPoint.ToString().Split(':');
            IPAddress.TryParse(caller[0], out System.Net.IPAddress lAddress);
            if (lAddress == null) throw new Exception("Invalid IP address");
            this.ipAddressCaller = lAddress.ToString();
            handler = state.workSocket;
            try
            {
                /* fino al 15.06.2021
                // Read data from the client socket.
                int bytesRead = handler.EndReceive(ar);

                if (bytesRead > 0)
                {
                    // There  might be more data, so store the data received so far.  
                    state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, bytesRead));

                    // Check for end-of-file tag. If it is not there, read
                    // more data.  
                    content = state.sb.ToString();
                    if (content.IndexOf("</SocketMessageStructure>") > -1)
                    {
                        // All the data has been read from the
                        // client. Display it on the console.  
                        // Console.WriteLine("Read {0} bytes from socket. \n Data : {1}", content.Length, content);
                        // if (_log != null) _log.Log(LogLevel.INFO, string.Format("{0} Read:{1} bytes from socket Data:{2}", logPrefisso, content.Length, content));
                        // Echo the data back to the client.  
                        if (Echo) Send(handler, content);
                        SocketMessageStructure sms = deserializedMessage(content);
                        if (_log != null)
                            //if ((this.SwDebug & _log.LOG_INFO) == _log.LOG_INFO)
                            _log.Log(LogLevel.INFO, string.Format("{0} The message form client as {1} is VALID", logPrefisso, sms.Command));
                        DataFromSocket?.Invoke(handler, sms);
                    }
                    else
                    {
                        // Not all data received. Get more.  
                        if (_log != null)
                            //if ((this.SwDebug & _log.LOG_INFO) == _log.LOG_INFO)
                            _log.Log(LogLevel.WARNING, string.Format("{0}The message form client as {1} is INVALID", logPrefisso, content));
                        handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadCallback), state);
                    }
                }
                
                */
                //15.06.2021  corretto gestione callback ricezione da socket. questo evita il warning di comando non riconosciuto.
                // Read data from the client socket.
                int bytesRead = handler.EndReceive(ar);

                if (bytesRead > 0)
                {
                    // There  might be more data, so store the data received so far.  
                    state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, bytesRead));
                    content = state.sb.ToString();
                    if (content.IndexOf("</SocketMessageStructure>") > -1)
                    {
                        if (Echo) Send(handler, content);
                        SocketMessageStructure sms = deserializedMessage(content);
                        if (_log != null)
                            _log.Log(LogLevel.INFO, string.Format("{0} The message form client as {1} is VALID", logPrefisso, sms.Command));
                        DataFromSocket?.Invoke(handler, sms);
                    }
                    else
                    {
                        handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadCallback), state);
                        if (_log != null)
                            _log.Log(LogLevel.DEBUG, string.Format("{0} waiting to receive data from the socket", logPrefisso));
                    }
                }
                else
                { 
                    if (state.sb.Length > 1)
                    {
                        content = state.sb.ToString();
                        if (content.IndexOf("</SocketMessageStructure>") > -1)
                        {
                            if (Echo) Send(handler, content);
                            SocketMessageStructure sms = deserializedMessage(content);
                            if (_log != null)
                                _log.Log(LogLevel.INFO, string.Format("{0} The message form client as {1} is VALID", logPrefisso, sms.Command));
                            DataFromSocket?.Invoke(handler, sms);
                        }
                        else
                        {
                            if (_log != null)
                                _log.Log(LogLevel.WARNING, string.Format("{0}The message form client as {1} is INVALID", logPrefisso, content));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                string msg = string.Format("{0} {1} {2}", logPrefisso, ClsMessaggiErrore.CustomMsg(ex, thisMethod), ((SocketException)ex).ErrorCode);
                /*
                if (_log != null) _log.Log(LogLevel.ERROR, msg);
                ErrorFromSocket?.Invoke(handler, ex.Message);
                */
                if (_log != null) _log.Log(LogLevel.ERROR, msg);
                throw new Exception(msg);
            }
        }
        private void ReadLocalAddressIP()
        {
            IPAddress[] ipv4Addresses = null;

            if (_log != null)
                //if ((this.SwDebug & _log.LOG_INFO) == _log.LOG_INFO)
                _log.Log(LogLevel.INFO, logPrefisso + "Acquisition of the IP address");
            ipv4Addresses = Array.FindAll(Dns.GetHostEntry(string.Empty).AddressList, a => a.AddressFamily == AddressFamily.InterNetwork);
            if (ipv4Addresses.Count() == 0)
                throw new Exception("No listening server socket was found");
            else 
                foreach (IPAddress ip in ipv4Addresses)
                {
                    ipAddressLocal.Add(ip);
                    if (_log != null)
                        //if ((this.SwDebug & _log.LOG_INFO) == _log.LOG_INFO)
                        _log.Log(LogLevel.INFO, string.Format("{0}IP address found: {1} ", logPrefisso, ip));
                }
        }
        private void SendCallback(IAsyncResult ar)
        {
            MethodBase thisMethod = MethodBase.GetCurrentMethod();
            try
            {
                // Retrieve the socket from the state object.  
                Socket handler = (Socket)ar.AsyncState;

                // Complete sending the data to the remote device.  
                int bytesSent = handler.EndSend(ar);
                if (_log != null)
                    //if ((this.SwDebug & _log.LOG_DEBUG) == _log.LOG_DEBUG)
                    _log.Log(LogLevel.DEBUG, string.Format("Sent {0} bytes to client.", bytesSent));
                Console.WriteLine("Sent {0} bytes to client.", bytesSent);

                handler.Shutdown(SocketShutdown.Both);
                handler.Close();
            }
            catch (Exception ex)
            {
                string msg = string.Format("{0} {1}", logPrefisso, ClsMessaggiErrore.CustomMsg(ex, thisMethod));
                // if (_log != null) _log.Log(LogLevel.ERROR, msg); else throw new Exception(msg, ex);
                throw new Exception(msg);
            }
        }
        #endregion "          * * * That's all folks  * * *           "
        #region "* * *  PROPERTIES  * * *"
        public bool Echo { get; set; }
        public Socket Handler { get { return this.handler; } set { this.handler = value; } }
        public Socket Listner { get { return this.listener; } }
        public int SrvPort { get { return this.port; } }
        public List<IPAddress> SrvIpAddress { get { return this.ipAddressLocal; } }
        public string CallerIpAddress{ get { return this.ipAddressCaller; } }
        public int SwDebug { get; set; }
        /// <summary>
        /// Traffic management token (02.04.2021)
        /// </summary>
        public long TokenSocket { get; set; }
        // public short IndexIP { get { return this.indexIP; } set { this.indexIP = value; } }
        #endregion "          * * * That's all folks  * * *           "
    }
    public class AsyncSocketThread
    {
        private const string logPrefisso = "<SOCKET>";
        private AsyncSocketListener asl;
        private Logger log;

        public void AsyncSocket()
        {
            MethodBase thisMethod = MethodBase.GetCurrentMethod();
            DateTime start = DateTime.Now;
#if DEBUG
            Console.WriteLine("Thread {0}: {1}, Priority {2}", Thread.CurrentThread.ManagedThreadId, Thread.CurrentThread.ThreadState, Thread.CurrentThread.Priority);
#endif
            do
            {
                try
                {
                    if (!IsStarted)
                    {
                        asl.StartListening(this.log);
                        IsStarted = true;
                        log.Log(LogLevel.INFO, logPrefisso + "The listener is listening on the socket channel");
                    }
                    else
                    {
                        if (log != null)
                            if ((this.SwDebug & log.LOG_DEBUG) == log.LOG_DEBUG)
                                log.Log(LogLevel.INFO, logPrefisso + "The listener is listening");
                        asl.Listening();
                        if (log != null)
                            if ((this.SwDebug & log.LOG_DEBUG) == log.LOG_DEBUG)
                                log.Log(LogLevel.INFO, logPrefisso + "The listener listened");

                    }
                    Thread.Sleep(this.Interval);
                }catch(Exception ex)
                {
                    string msg = string.Format("{0} {1} {2}", logPrefisso, ClsMessaggiErrore.CustomMsg(ex, thisMethod), ((SocketException)ex).ErrorCode);
                    if (log != null) log.Log(LogLevel.ERROR, logPrefisso + msg); else throw new Exception(msg, ex);

                }
#if DEBUG
                // Console.WriteLine("Thread {0}: Elapsed {1:N2} seconds", Thread.CurrentThread.ManagedThreadId, sw.ElapsedMilliseconds / 1000.0);
#endif
            } while (!StopThread); // (sw.ElapsedMilliseconds <= interval);
            StopThread = false;
            if (log != null) this.log.Log(LogLevel.INFO, logPrefisso + "(8) Thread async socket server STOPED");
        }
        public int Interval { get; set; }
        public Logger Log { get { return this.log; } set { this.log = value; } }
        public bool IsStarted { get; set; }
        public bool StopThread { get; set; }
        public int SwDebug { get; set; }
        public AsyncSocketListener AsyncSocketListener { get { return this.asl; } set { this.asl = value; } }
    }
}
