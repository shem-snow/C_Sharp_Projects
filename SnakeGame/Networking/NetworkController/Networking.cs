using System;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;

namespace NetworkUtil;

/// <summary>
/// This is the STATIC (library) class that defines networking behavior such as starting, stopping, connecting, and 
/// sending/receiving messages. It will be used by independent Server and Client classes for the sake of separating the 
/// concerns of network behavior from their individual functions.
/// 
/// Authors: Ronald Foster, Shem Snow
/// Last Modified on 11/11/2022
/// </summary>
public static class Networking
{
    #region Server-Side

    /// <summary>
    /// Starts a TcpListener on the specified port and starts an event-loop to accept new clients.
    /// The event-loop is started with BeginAcceptSocket and uses AcceptNewClient as the callback.
    /// AcceptNewClient will continue the event-loop.
    /// </summary>
    /// <param name="toCall">The method to call when a new connection is made</param>
    /// <param name="port">The the port to listen on</param>
    public static TcpListener StartServer(Action<SocketState> toCall, int port)
    {
        // Start TcpListener
        IPAddress ipAddress = new IPAddress(new byte[] { 127, 0, 0, 1 });
        TcpListener listener = new TcpListener(ipAddress, port);
        listener.Start();
        // Begin Event-Loop
        Tuple<TcpListener, Action<SocketState>> ar = new Tuple<TcpListener, Action<SocketState>>(listener, toCall);
        listener.BeginAcceptSocket(AcceptNewClient, ar);

        return listener;
    }

    /// <summary>
    /// To be used as the callback for accepting a new client that was initiated by StartServer, and 
    /// continues an event-loop to accept additional clients.
    ///
    /// Uses EndAcceptSocket to finalize the connection and create a new SocketState. The SocketState's
    /// OnNetworkAction should be set to the delegate that was passed to StartServer.
    /// Then invokes the OnNetworkAction delegate with the new SocketState so the user can take action. 
    /// 
    /// If anything goes wrong during the connection process (such as the server being stopped externally), 
    /// the OnNetworkAction delegate should be invoked with a new SocketState with its ErrorOccurred flag set to true 
    /// and an appropriate message placed in its ErrorMessage field. The event-loop should not continue if
    /// an error occurs.
    ///
    /// If an error does not occur, after invoking OnNetworkAction with the new SocketState, an event-loop to accept 
    /// new clients should be continued by calling BeginAcceptSocket again with this method as the callback.
    /// </summary>
    /// <param name="ar">The object asynchronously passed via BeginAcceptSocket. It must contain a tuple with 
    /// 1) a delegate so the user can take action (a SocketState Action), and 2) the TcpListener</param>
    private static void AcceptNewClient(IAsyncResult ar)
    {
        // Initialize State
        Tuple<TcpListener, Action<SocketState>> asyncResult = (Tuple<TcpListener, Action<SocketState>>)ar.AsyncState!;
        Action<SocketState> networkAction = asyncResult.Item2;
        try
        {
            // Finalize Connection
            Socket socket = asyncResult.Item1.EndAcceptSocket(ar);
            SocketState state = new SocketState(asyncResult.Item2, socket);
            // Allow User to Take Action
            state.OnNetworkAction(state);
            // Event-Loop to Allow new Clients
            asyncResult.Item1.BeginAcceptSocket(AcceptNewClient, asyncResult);
        }
        catch (Exception ex)
        {   // Handle Errors
            ErrorState(networkAction, ex.Message);
        }
    }

    /// <summary>
    /// Stops the given TcpListener.
    /// </summary>
    public static void StopServer(TcpListener listener)
    {
        listener.Stop();
    }

    #endregion

    #region Client-Side

    /// <summary>
    /// Begins the asynchronous process of connecting to a server via BeginConnect, 
    /// and using ConnectedCallback as the method to finalize the connection once it's made.
    /// 
    /// If anything goes wrong during the connection process, toCall should be invoked 
    /// with a new SocketState with its ErrorOccurred flag set to true and an appropriate message 
    /// placed in its ErrorMessage field. Depending on when the error occurs, this should happen either
    /// in this method or in ConnectedCallback.
    ///
    /// This connection process should timeout and produce an error (as discussed above) 
    /// if a connection can't be established within 3 seconds of starting BeginConnect.
    /// 
    /// </summary>
    /// <param name="toCall">The action to take once the connection is open or an error occurs</param>
    /// <param name="hostName">The server to connect to</param>
    /// <param name="port">The port on which the server is listening</param>
    public static void ConnectToServer(Action<SocketState> toCall, string hostName, int port)
    {
        // Establish the remote endpoint for the socket.
        IPHostEntry ipHostInfo;
        IPAddress ipAddress = IPAddress.None;

        // Determine if the server address is a URL or an IP
        try
        {
            ipHostInfo = Dns.GetHostEntry(hostName);
            bool foundIPV4 = false;
            foreach (IPAddress addr in ipHostInfo.AddressList)
                if (addr.AddressFamily != AddressFamily.InterNetworkV6)
                {
                    foundIPV4 = true;
                    ipAddress = addr;
                    break;
                }
            // Didn't find any IPV4 addresses
            if (!foundIPV4)
            {
                throw new Exception("Didn't find any IPV4 addresses");
            }
        }
        catch (Exception)
        {
            // see if host name is a valid ipaddress
            try
            {
                ipAddress = IPAddress.Parse(hostName);
            }
            catch (Exception)
            {
                ErrorState(toCall, $"{hostName} is an invalid host name or address");
                return;
            }
        }

        // Create a TCP/IP socket.
        Socket socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        socket.NoDelay = true;
        // Attempt connection
        Tuple<Action<SocketState>, Socket> connectionState = new Tuple<Action<SocketState>, Socket>(toCall, socket);
        IAsyncResult ar = socket.BeginConnect(ipAddress, port, ConnectedCallback, connectionState);
        // Handle Timeout
        ar.AsyncWaitHandle.WaitOne(3000, true);

        lock (socket)
        {
            if (!socket.Connected)
            {
                ErrorState(toCall, "Connection to the server timed out");
            }
        }
    }

    /// <summary>
    /// To be used as the callback for finalizing a connection process that was initiated by ConnectToServer.
    ///
    /// Uses EndConnect to finalize the connection.
    /// 
    /// As stated in the ConnectToServer documentation, if an error occurs during the connection process,
    /// either this method or ConnectToServer should indicate the error appropriately.
    /// 
    /// If a connection is successfully established, invokes the toCall Action that was provided to ConnectToServer (above)
    /// with a new SocketState representing the new connection.
    /// 
    /// </summary>
    /// <param name="ar">The object asynchronously passed via BeginConnect</param>
    private static void ConnectedCallback(IAsyncResult ar)
    {
        // Finalize Connection
        Tuple<Action<SocketState>, Socket> connectionState = (Tuple<Action<SocketState>, Socket>)ar.AsyncState!;
        try
        {
            connectionState.Item2.EndConnect(ar);
        }
        catch (Exception ex)
        {
            ErrorState(connectionState.Item1, ex.Message);
            return;
        }
        SocketState state = new SocketState(connectionState.Item1, connectionState.Item2);
        state.OnNetworkAction(state);
    }

    #endregion

    #region Server and Client Common

    /// <summary>
    /// Begins the asynchronous process of receiving data via BeginReceive, using ReceiveCallback 
    /// as the callback to finalize the receive and store data once it has arrived.
    /// The object passed to ReceiveCallback via the AsyncResult should be the SocketState.
    /// 
    /// If anything goes wrong during the receive process, the SocketState's ErrorOccurred flag should 
    /// be set to true, and an appropriate message placed in ErrorMessage, then the SocketState's
    /// OnNetworkAction should be invoked. Depending on when the error occurs, this should happen either
    /// in this method or in ReceiveCallback.
    /// </summary>
    /// <param name="state">The SocketState to begin receiving</param>
    public static void GetData(SocketState state)
    {
        // Start Receiving Data
        try
        {
            state.TheSocket.BeginReceive(state.buffer, 0, state.buffer.Length,
                SocketFlags.None, ReceiveCallback, state);
        }
        catch (Exception ex)
        {   // Handle Errors
            ErrorState(state, ex.Message);
        }
    }

    /// <summary>
    /// To be used as the callback for finalizing a receive operation that was initiated by GetData.
    /// 
    /// Uses EndReceive to finalize the receive.
    ///
    /// As stated in the GetData documentation, if an error occurs during the receive process,
    /// either this method or GetData should indicate the error appropriately.
    /// 
    /// If data is successfully received:
    ///  (1) Read the characters as UTF8 and put them in the SocketState's unprocessed data buffer (its string builder).
    ///      This must be done in a thread-safe manner with respect to the SocketState methods that access or modify its 
    ///      string builder.
    ///  (2) Call the saved delegate (OnNetworkAction) allowing the user to deal with this data.
    /// </summary>
    /// <param name="ar"> 
    /// This contains the SocketState that is stored with the callback when the initial BeginReceive is called.
    /// </param>
    private static void ReceiveCallback(IAsyncResult ar)
    {
        SocketState state = (SocketState)ar.AsyncState!;
        int numBytes = 0;

        // Finalize Receive
        try
        {
            numBytes = state.TheSocket.EndReceive(ar);
        }
        catch (Exception ex)
        {   // Handle Errors
            if (numBytes == 0)
            {
                ErrorState(state, "Number of bytes was 0");
            }
            else
            {
                ErrorState(state, ex.Message);
            }
            return;
        }

        // Read Data
        string message = "";
        lock (state)
        {
            message = Encoding.UTF8.GetString(state.buffer, 0, numBytes);
            state.data.Append(message);
        }
        state.OnNetworkAction(state);
    }

    /// <summary>
    /// Begin the asynchronous process of sending data via BeginSend, using SendCallback to finalize the send process.
    /// 
    /// If the socket is closed, does not attempt to send.
    /// 
    /// If a send fails for any reason, this method ensures that the Socket is closed before returning.
    /// </summary>
    /// <param name="socket">The socket on which to send the data</param>
    /// <param name="data">The string to send</param>
    /// <returns>True if the send process was started, false if an error occurs or the socket is already closed</returns>
    public static bool Send(Socket socket, string data)
    {
        // Validate Socket (see if it's closed)
        lock(socket)
        {
            if (!socket.Connected)
            {
                return false;
            }
        } 

        // Format the data to send
        byte[] msgBuffer = Encoding.UTF8.GetBytes(data);

        // Begin Sending Data with "SendCallback" as the callback.
        try
        {
            socket.BeginSend(msgBuffer, 0, msgBuffer.Length, SocketFlags.None, SendCallback, socket);
        }
        catch (Exception)
        //  return a boolean that indicates whether or not the sending was attempted.
        {
            socket.Close();
            return false;
        }
        return true;
    }

    /// <summary>
    /// To be used as the callback for finalizing a send operation that was initiated by Send.
    ///
    /// Uses EndSend to finalize the send.
    /// 
    /// This method must not throw, even if an error occurred during the Send operation.
    /// </summary>
    /// <param name="ar">
    /// This is the Socket (not SocketState) that is stored with the callback when
    /// the initial BeginSend is called.
    /// </param>
    private static void SendCallback(IAsyncResult ar)
    {
        // Finalize Send
        Socket socket = (Socket)ar.AsyncState!;
        try
        {
            socket.EndSend(ar);
        }
        catch (Exception)
        {
            // intentionally blank
        }
    }

    /// <summary>
    /// Begin the asynchronous process of sending data via BeginSend, using SendAndCloseCallback to finalize the send process.
    /// This variant closes the socket in the callback once complete. This is useful for HTTP servers.
    /// 
    /// If the socket is closed, does not attempt to send.
    /// 
    /// If a send fails for any reason, this method ensures that the Socket is closed before returning.
    /// </summary>
    /// <param name="socket">The socket on which to send the data</param>
    /// <param name="data">The string to send</param>
    /// <returns>True if the send process was started, false if an error occurs or the socket is already closed</returns>
    public static bool SendAndClose(Socket socket, string data)
    {
        // If the socket is closed, don't even try to send data.
        lock (socket)
        {
            if (!socket.Connected)
            {
                return false;
            }
        }

        // Save the data into a buffer.
        byte[] buffer = Encoding.UTF8.GetBytes(data);
        // Begin Sending Data
        try
        {
            socket.BeginSend(buffer, 0, buffer.Length, SocketFlags.None, SendAndCloseCallback, socket);
        }
        catch
        {   // Failed to start send
            socket.Close();
            return false;
        }

        // Successfully started Send
        return true;
    }

    /// <summary>
    /// To be used as the callback for finalizing a send operation that was initiated by SendAndClose.
    ///
    /// Uses EndSend to finalize the send, then closes the socket.
    /// 
    /// This method must not throw, even if an error occurred during the Send operation.
    /// 
    /// This method ensures that the socket is closed before returning.
    /// </summary>
    /// <param name="ar">
    /// This is the Socket (not SocketState) that is stored with the callback when
    /// the initial BeginSend is called.
    /// </param>
    private static void SendAndCloseCallback(IAsyncResult ar)
    {
        Socket socket = (Socket)ar.AsyncState!;
        // Finalize Send
        try
        {
            socket.EndSend(ar);
        }
        catch
        {
            // Intentionally blank because this method should never throw.
        }
        // Close the Socket
        socket.Close();
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Sets the argued SocketState into a state of error with the argued message 
    /// and calls its OnNetworkAction
    /// </summary>
    /// <param name="state">SocketState that is erroneous</param>
    /// <param name="errorMsg">Message explaining the error</param>
    private static void ErrorState(SocketState state, string errorMsg)
    {
        state.ErrorOccurred = true;
        state.ErrorMessage = errorMsg;
        state.OnNetworkAction(state);
    }

    /// <summary>
    /// Constructs an erroneous socketstate with the argued network action and error message. 
    /// Then invokes the networkAction.
    /// </summary>
    /// <param name="networkAction"></param>
    /// <param name="errorMsg"></param>
    private static void ErrorState(Action<SocketState> networkAction, string errorMsg)
    {
        SocketState state = new SocketState(networkAction, errorMsg);
        state.OnNetworkAction(state);
    }

    #endregion
}
