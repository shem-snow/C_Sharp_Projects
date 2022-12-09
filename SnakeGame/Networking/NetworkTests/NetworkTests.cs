using NetworkUtil;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography.X509Certificates;
using System.Text;


namespace NetworkTests
{
    /// <summary>
    /// Tester class for all the Networking.cs methods.
    /// 
    /// Authors: Ronald Foster, Shem Snow
    /// Last Modified on 11/11/2022
    /// </summary>
    [TestClass]
    public class NetworkTests
    {
        // When testing network code, we have some necessary global state,
        // since open sockets are system-wide (managed by the OS)
        // Therefore, we need some per-test setup and cleanup
        private TcpListener? testListener;
        private SocketState? testLocalSocketState, testRemoteSocketState;

        #region Initialization and Termination Tests

        [TestInitialize]
        public void Init()
        {
            testListener = null;
            testLocalSocketState = null;
            testRemoteSocketState = null;
        }


        [TestCleanup]
        public void Cleanup()
        {
            StopTestServer(testListener, testLocalSocketState, testRemoteSocketState);
        }


        private void StopTestServer(TcpListener? listener, SocketState? socket1, SocketState? socket2)
        {
            try
            {
                // '?.' is just shorthand for null checks
                listener?.Stop();
                socket1?.TheSocket?.Shutdown(SocketShutdown.Both);
                socket1?.TheSocket?.Close();
                socket2?.TheSocket?.Shutdown(SocketShutdown.Both);
                socket2?.TheSocket?.Close();
            }
            // Do nothing with the exception, since shutting down the server will likely result in 
            // a prematurely closed socket
            // If the timeout is long enough, the shutdown should succeed
            catch (Exception) { }
        }



        public void SetupTestConnections(bool clientSide,
          out TcpListener listener, out SocketState local, out SocketState remote)
        {
            SocketState? tempLocal, tempRemote;
            if (clientSide)
            {
                NetworkTestHelper.SetupSingleConnectionTest(
                  out listener,
                  out tempLocal,    // local becomes client
                  out tempRemote);  // remote becomes server
            }
            else
            {
                NetworkTestHelper.SetupSingleConnectionTest(
                  out listener,
                  out tempRemote,   // remote becomes client
                  out tempLocal);   // local becomes server
            }

            Assert.IsNotNull(tempLocal);
            Assert.IsNotNull(tempRemote);
            local = tempLocal;
            remote = tempRemote;
        }

        #endregion

        #region Basic Connectivity Tests

        [TestMethod]
        public void TestConnect()
        {
            NetworkTestHelper.SetupSingleConnectionTest(out testListener, out testLocalSocketState, out testRemoteSocketState);
            Assert.IsNotNull(testLocalSocketState);
            Assert.IsNotNull(testRemoteSocketState);

            Assert.IsTrue(testRemoteSocketState.TheSocket.Connected);
            Assert.IsTrue(testLocalSocketState.TheSocket.Connected);

            Assert.AreEqual("127.0.0.1:2112", testLocalSocketState.TheSocket.RemoteEndPoint?.ToString());
        }


        [TestMethod]
        public void TestConnectNoServer()
        {
            bool isCalled = false;

            void saveClientState(SocketState x)
            {
                isCalled = true;
                testLocalSocketState = x;
            }

            // Try to connect without setting up a server first.
            Networking.ConnectToServer(saveClientState, "localhost", 2112);
            NetworkTestHelper.WaitForOrTimeout(() => isCalled, NetworkTestHelper.timeout);

            Assert.IsTrue(isCalled);
            Assert.IsTrue(testLocalSocketState?.ErrorOccurred);
        }


        [TestMethod]
        public void TestConnectTimeout()
        {
            bool isCalled = false;

            void saveClientState(SocketState x)
            {
                isCalled = true;
                testLocalSocketState = x;
            }

            Networking.ConnectToServer(saveClientState, "google.com", 2112);

            // The connection should timeout after 3 seconds. NetworkTestHelper.timeout is 5 seconds.
            NetworkTestHelper.WaitForOrTimeout(() => isCalled, NetworkTestHelper.timeout);

            Assert.IsTrue(isCalled);
            Assert.IsTrue(testLocalSocketState?.ErrorOccurred);
        }


        [TestMethod]
        public void TestConnectCallsDelegate()
        {
            bool serverActionCalled = false;
            bool clientActionCalled = false;

            void saveServerState(SocketState x)
            {
                testLocalSocketState = x;
                serverActionCalled = true;
            }

            void saveClientState(SocketState x)
            {
                testRemoteSocketState = x;
                clientActionCalled = true;
            }

            testListener = Networking.StartServer(saveServerState, 2112);
            Networking.ConnectToServer(saveClientState, "localhost", 2112);
            NetworkTestHelper.WaitForOrTimeout(() => serverActionCalled, NetworkTestHelper.timeout);
            NetworkTestHelper.WaitForOrTimeout(() => clientActionCalled, NetworkTestHelper.timeout);

            Assert.IsTrue(serverActionCalled);
            Assert.IsTrue(clientActionCalled);
        }


        /// <summary>
        /// This is an example of a parameterized test. 
        /// DataRow(true) and DataRow(false) means this test will be 
        /// invoked once with an argument of true, and once with false.
        /// This way we can test your Send method from both
        /// client and server sockets. In theory, there should be no 
        /// difference, but odd things can happen if you save static
        /// state (such as sockets) in your networking library.
        /// </summary>
        /// <param name="clientSide"></param>
        [DataRow(true)]
        [DataRow(false)]
        [DataTestMethod]
        public void TestDisconnectLocalThenSend(bool clientSide)
        {
            SetupTestConnections(clientSide, out testListener, out testLocalSocketState, out testRemoteSocketState);

            testLocalSocketState.TheSocket.Shutdown(SocketShutdown.Both);

            // No assertions, but the following should not result in an unhandled exception
            Networking.Send(testLocalSocketState.TheSocket, "a");
        }

        [TestMethod]
        public void TestHostNameConnectionIPV6()
        {
            bool emptyActionCalled = false;
            void emptyAction (SocketState x)
            {
                emptyActionCalled = true;
                testLocalSocketState = x;
            }

            testListener = Networking.StartServer(emptyAction, 2112);
            Networking.ConnectToServer(emptyAction, "0:0:0:0:0:0:0:1", 2112);
            NetworkTestHelper.WaitForOrTimeout(() => emptyActionCalled, NetworkTestHelper.timeout);
            Assert.IsTrue(emptyActionCalled);
        }

        #endregion

        #region Begin Send/Receive Tests

        // In these tests, "local" means the SocketState doing the sending,
        // and "remote" is the one doing the receiving.
        // Each test will run twice, swapping the sender and receiver between
        // client and server, in order to defeat statically-saved SocketStates
        [DataRow(true)]
        [DataRow(false)]
        [DataTestMethod]
        public void TestSendTinyMessage(bool clientSide)
        {
            SetupTestConnections(clientSide, out testListener, out testLocalSocketState, out testRemoteSocketState);

            // Set the action to do nothing
            testLocalSocketState.OnNetworkAction = x => { };
            testRemoteSocketState.OnNetworkAction = x => { };

            Networking.Send(testLocalSocketState.TheSocket, "a");

            Networking.GetData(testRemoteSocketState);

            // Note that waiting for data like this is *NOT* how the networking library is 
            // intended to be used. This is only for testing purposes.
            // Normally, you would provide an OnNetworkAction that handles the data.
            NetworkTestHelper.WaitForOrTimeout(() => testRemoteSocketState.GetData().Length > 0, NetworkTestHelper.timeout);

            Assert.AreEqual("a", testRemoteSocketState.GetData());
        }

        [DataRow(true)]
        [DataRow(false)]
        [DataTestMethod]
        public void TestNoEventLoop(bool clientSide)
        {
            SetupTestConnections(clientSide, out testListener, out testLocalSocketState, out testRemoteSocketState);

            int calledCount = 0;

            // This OnNetworkAction will not ask for more data after receiving one message,
            // so it should only ever receive one message
            testLocalSocketState.OnNetworkAction = (x) => calledCount++;

            Networking.Send(testRemoteSocketState.TheSocket, "a");
            Networking.GetData(testLocalSocketState);
            // Note that waiting for data like this is *NOT* how the networking library is 
            // intended to be used. This is only for testing purposes.
            // Normally, you would provide an OnNetworkAction that handles the data.
            NetworkTestHelper.WaitForOrTimeout(() => testLocalSocketState.GetData().Length > 0, NetworkTestHelper.timeout);

            // Send a second message (which should not increment calledCount)
            Networking.Send(testRemoteSocketState.TheSocket, "a");
            NetworkTestHelper.WaitForOrTimeout(() => false, NetworkTestHelper.timeout);

            Assert.AreEqual(1, calledCount);
        }


        [DataRow(true)]
        [DataRow(false)]
        [DataTestMethod]
        public void TestDelayedSends(bool clientSide)
        {
            SetupTestConnections(clientSide, out testListener, out testLocalSocketState, out testRemoteSocketState);

            // Set the action to do nothing
            testLocalSocketState.OnNetworkAction = x => { };
            testRemoteSocketState.OnNetworkAction = x => { };

            Networking.Send(testLocalSocketState.TheSocket, "a");
            Networking.GetData(testRemoteSocketState);
            // Note that waiting for data like this is *NOT* how the networking library is 
            // intended to be used. This is only for testing purposes.
            // Normally, you would provide an OnNetworkAction that handles the data.
            NetworkTestHelper.WaitForOrTimeout(() => testRemoteSocketState.GetData().Length > 0, NetworkTestHelper.timeout);

            Networking.Send(testLocalSocketState.TheSocket, "b");
            Networking.GetData(testRemoteSocketState);
            // Note that waiting for data like this is *NOT* how the networking library is 
            // intended to be used. This is only for testing purposes.
            // Normally, you would provide an OnNetworkAction that handles the data.
            NetworkTestHelper.WaitForOrTimeout(() => testRemoteSocketState.GetData().Length > 1, NetworkTestHelper.timeout);

            Assert.AreEqual("ab", testRemoteSocketState.GetData());
        }


        [DataRow(true)]
        [DataRow(false)]
        [DataTestMethod]
        public void TestEventLoop(bool clientSide)
        {
            SetupTestConnections(clientSide, out testListener, out testLocalSocketState, out testRemoteSocketState);

            int calledCount = 0;

            // This OnNetworkAction asks for more data, creating an event loop
            testLocalSocketState.OnNetworkAction = (x) =>
            {
                if (x.ErrorOccurred)
                    return;
                calledCount++;
                Networking.GetData(x);
            };

            Networking.Send(testRemoteSocketState.TheSocket, "a");
            Networking.GetData(testLocalSocketState);
            NetworkTestHelper.WaitForOrTimeout(() => calledCount == 1, NetworkTestHelper.timeout);

            Networking.Send(testRemoteSocketState.TheSocket, "a");
            NetworkTestHelper.WaitForOrTimeout(() => calledCount == 2, NetworkTestHelper.timeout);

            Assert.AreEqual(2, calledCount);
        }


        [DataRow(true)]
        [DataRow(false)]
        [DataTestMethod]
        public void TestChangeOnNetworkAction(bool clientSide)
        {
            SetupTestConnections(clientSide, out testListener, out testLocalSocketState, out testRemoteSocketState);

            int firstCalledCount = 0;
            int secondCalledCount = 0;

            // This is an example of a nested method (just another way to make a quick delegate)
            void firstOnNetworkAction(SocketState state)
            {
                if (state.ErrorOccurred)
                    return;
                firstCalledCount++;
                state.OnNetworkAction = secondOnNetworkAction;
                Networking.GetData(testLocalSocketState);
            }

            void secondOnNetworkAction(SocketState state)
            {
                secondCalledCount++;
            }

            // Change the OnNetworkAction after the first invokation
            testLocalSocketState.OnNetworkAction = firstOnNetworkAction;

            Networking.Send(testRemoteSocketState.TheSocket, "a");
            Networking.GetData(testLocalSocketState);
            NetworkTestHelper.WaitForOrTimeout(() => firstCalledCount == 1, NetworkTestHelper.timeout);

            Networking.Send(testRemoteSocketState.TheSocket, "a");
            NetworkTestHelper.WaitForOrTimeout(() => secondCalledCount == 1, NetworkTestHelper.timeout);

            Assert.AreEqual(1, firstCalledCount);
            Assert.AreEqual(1, secondCalledCount);
        }



        [DataRow(true)]
        [DataRow(false)]
        [DataTestMethod]
        public void TestReceiveRemovesAll(bool clientSide)
        {
            SetupTestConnections(clientSide, out testListener, out testLocalSocketState, out testRemoteSocketState);

            StringBuilder localCopy = new StringBuilder();

            void removeMessage(SocketState state)
            {
                if (state.ErrorOccurred)
                    return;
                localCopy.Append(state.GetData());
                state.RemoveData(0, state.GetData().Length);
                Networking.GetData(state);
            }

            testLocalSocketState.OnNetworkAction = removeMessage;

            // Start a receive loop
            Networking.GetData(testLocalSocketState);

            for (int i = 0; i < 10000; i++)
            {
                char c = (char)('a' + (i % 26));
                Networking.Send(testRemoteSocketState.TheSocket, "" + c);
            }

            NetworkTestHelper.WaitForOrTimeout(() => localCopy.Length == 10000, NetworkTestHelper.timeout);

            // Reconstruct the original message outside the send loop
            // to (in theory) make the send operations happen more rapidly.
            StringBuilder message = new StringBuilder();
            for (int i = 0; i < 10000; i++)
            {
                char c = (char)('a' + (i % 26));
                message.Append(c);
            }

            Assert.AreEqual(message.ToString(), localCopy.ToString());
        }


        [DataRow(true)]
        [DataRow(false)]
        [DataTestMethod]
        public void TestReceiveRemovesPartial(bool clientSide)
        {
            SetupTestConnections(clientSide, out testListener, out testLocalSocketState, out testRemoteSocketState);

            const string toSend = "abcdefghijklmnopqrstuvwxyz";

            // Use a static seed for reproducibility
            Random rand = new Random(0);

            StringBuilder localCopy = new StringBuilder();

            void removeMessage(SocketState state)
            {
                if (state.ErrorOccurred)
                    return;
                int numToRemove = rand.Next(state.GetData().Length);
                localCopy.Append(state.GetData().Substring(0, numToRemove));
                state.RemoveData(0, numToRemove);
                Networking.GetData(state);
            }

            testLocalSocketState.OnNetworkAction = removeMessage;

            // Start a receive loop
            Networking.GetData(testLocalSocketState);

            for (int i = 0; i < 1000; i++)
            {
                Networking.Send(testRemoteSocketState.TheSocket, toSend);
            }

            // Wait a while
            NetworkTestHelper.WaitForOrTimeout(() => false, NetworkTestHelper.timeout);

            localCopy.Append(testLocalSocketState.GetData());

            // Reconstruct the original message outside the send loop
            // to (in theory) make the send operations happen more rapidly.
            StringBuilder message = new StringBuilder();
            for (int i = 0; i < 1000; i++)
            {
                message.Append(toSend);
            }

            Assert.AreEqual(message.ToString(), localCopy.ToString());
        }



        [DataRow(true)]
        [DataRow(false)]
        [DataTestMethod]
        public void TestReceiveHugeMessage(bool clientSide)
        {
            SetupTestConnections(clientSide, out testListener, out testLocalSocketState, out testRemoteSocketState);

            testLocalSocketState.OnNetworkAction = (x) =>
            {
                if (x.ErrorOccurred)
                    return;
                Networking.GetData(x);
            };

            Networking.GetData(testLocalSocketState);

            StringBuilder message = new StringBuilder();
            message.Append('a', (int)(SocketState.BufferSize * 7.5));

            Networking.Send(testRemoteSocketState.TheSocket, message.ToString());

            NetworkTestHelper.WaitForOrTimeout(() => testLocalSocketState.GetData().Length == message.Length, NetworkTestHelper.timeout);

            Assert.AreEqual(message.ToString(), testLocalSocketState.GetData());
        }

        #endregion

        #region Our Additional Initialization and Termination Tests

        [TestMethod]
        public void ConnectToAnInvalidIP()
        {
            // Initialize variables for connection
            bool errorOccured = false;
            bool emptyActionCalled = false;
            void emptyAction(SocketState x)
            {
                errorOccured = x.ErrorOccurred;
                emptyActionCalled = true;
                testLocalSocketState = x;
            }

            // Attempt To connect
            testListener = Networking.StartServer(emptyAction, 2112);
            Networking.ConnectToServer(emptyAction, "some Invalid host", 2112);
            NetworkTestHelper.WaitForOrTimeout(() => emptyActionCalled, NetworkTestHelper.timeout);

            // Assertions
            Assert.IsTrue(emptyActionCalled);
            Assert.IsTrue(errorOccured);
        }

        [TestMethod]
        public void TestStopNetworkServer()
        {
            try
            {
                // Create a TcpListener and connect a client to it.
                int port = 1024;
                TcpListener listener = Networking.StartServer(s => { }, port);
                TcpClient client = new("127.0.0.1", port);
                Assert.IsTrue(client.Connected);

                // Stop the listener. 
                Networking.StopServer(listener);
                // Attempting to connect clients to it will now throw an ExtendedSocketException.
                client = new TcpClient("127.0.0.1", port);
            }
            catch (SocketException e) {

                // ExtendedSocketException is a private class so we have to check equality via the exception's fullname.
                string? exType = e.GetType().FullName;

                // If it matches then we have an ExtendedSocketException. The test passes, otherwise it fails
                if (exType is null || !exType.Equals("System.Net.Internals.SocketExceptionFactory+ExtendedSocketException"))
                    throw new Exception(e.Message);
            } // This ExtendedSocketException
            catch (Exception) { Assert.Fail(""); }

        }

        #endregion

        #region Our Additional Send and Receive Tests

        [TestMethod]
        public void GetInvalidData()
        {
            SetupTestConnections(true, out testListener, out testLocalSocketState, out testRemoteSocketState);
            
            // Set the action to do nothing and close the sockets
            testLocalSocketState.OnNetworkAction = x => { };
            testLocalSocketState.TheSocket.Close();
            testRemoteSocketState.OnNetworkAction = x => { };
            testRemoteSocketState.TheSocket.Close();

            Networking.GetData(testRemoteSocketState);

            // Note that waiting for data like this is *NOT* how the networking library is 
            // intended to be used. This is only for testing purposes.
            // Normally, you would provide an OnNetworkAction that handles the data.
            NetworkTestHelper.WaitForOrTimeout(() => testRemoteSocketState.GetData().Length > 0, NetworkTestHelper.timeout);

            Assert.IsTrue(testRemoteSocketState.ErrorOccurred);
        }

        [TestMethod]
        public void TestSendAndClose()
        {
            SetupTestConnections(true, out testListener, out testLocalSocketState, out testRemoteSocketState);

            // Set the OnNetworkAction to only return if no errors occured.
            testLocalSocketState.OnNetworkAction = (x) =>
            {
                if (x.ErrorOccurred)
                    return;
                Networking.GetData(x);
            };

            // Get and save the data to send
            Networking.GetData(testLocalSocketState);
            StringBuilder message = new StringBuilder();
            message.Append('a', (int)(SocketState.BufferSize * 7.5));

            // Send it
            Networking.SendAndClose(testRemoteSocketState.TheSocket, message.ToString());
            NetworkTestHelper.WaitForOrTimeout(() => testLocalSocketState.GetData().Length == message.Length, NetworkTestHelper.timeout);

            // Assertions: the data was received and the socket is no longer connected.
            Assert.AreEqual(message.ToString(), testLocalSocketState.GetData());
            Assert.IsFalse(testRemoteSocketState.TheSocket.Connected);
        }

        [TestMethod]
        public void TestSendAndCloseWithClosedSocket()
        {
            // Set up the connections and close the socket.
            SetupTestConnections(true, out testListener, out testLocalSocketState, out testRemoteSocketState);
            testRemoteSocketState.TheSocket.Close();
            bool sent = true;
            
            // Attempt to send.
            sent = Networking.SendAndClose(testRemoteSocketState.TheSocket, "");
            NetworkTestHelper.WaitForOrTimeout(() => !sent, NetworkTestHelper.timeout);

            // The message should fail to send.
            Assert.IsFalse(sent);
        }

        #endregion
    }

}