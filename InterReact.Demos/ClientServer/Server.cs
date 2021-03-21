﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using InterReact.Core;
using InterReact.Enums;
using InterReact.Utility;
using RxSockets;
using System.Reactive.Linq;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Reactive.Threading.Tasks;

#nullable enable

namespace CoreClientServer
{
    internal class Server
    {
        private readonly IPEndPoint EndPoint;
        private readonly ILogger<RxSocketServer> Logger;
        private readonly Limiter Limiter = new Limiter();

        internal Server(int port, ILogger<RxSocketServer> logger)
        {
            EndPoint = new IPEndPoint(IPAddress.IPv6Any, port);
            Logger = logger;
        }

        internal async Task Run()
        {
            //var server = EndPoint.CreateRxSocketServer(Logger);
            var server = EndPoint.CreateRxSocketServer();

            Logger.LogInformation("Waiting for client.");
            var accept = await server.AcceptObservable.FirstAsync();
            Logger.LogInformation("Client connection accepted.");

            var firstString = await accept.ReceiveObservable.ToStrings().FirstAsync();
            if (firstString != "API")
                throw new InvalidDataException("'API' not received.");
            Logger.LogInformation("Received 'API'.");

            // Start receiving with length prefix.
            // hangs on next line
            var messages1 = await accept.ReceiveObservable.ToByteArrayOfLengthPrefix().ToStringArray().FirstAsync();
            var versions = messages1.Single();

            if (!versions.StartsWith("v"))
                throw new InvalidDataException("Versions not received.");
            Logger.LogInformation($"Received supported server versions: '{versions}'.");

            var messages2 = await accept.ReceiveObservable.ToByteArrayOfLengthPrefix().ToStringArray().FirstAsync();

            if (messages2[0] != "71") // receive StartApi message
                throw new InvalidDataException("StartApi message not received.");
            Logger.LogInformation("Received StartApi message.");

            new RequestMessage(accept, Limiter)
                .Write(149) // server version
                .Write(DateTime.Now.ToString("yyyyMMdd HH:mm:ss XXX"))
                .Send();

            new RequestMessage(accept, Limiter)
                .Write("15")
                .Write("1")
                .Write("123,456,789")
                .Send();

            new RequestMessage(accept, Limiter)
                .Write("9")
                .Write("1")
                .Write("10")
                .Send();

            Logger.LogInformation("Client login complete.");

            ////////////////////////////////////////////////////

            var obs = accept.ReceiveObservable.ToByteArrayOfLengthPrefix().ToStringArray().Publish().RefCount();

            // receive test start signal
            await obs.FirstAsync();

            var watch = new Stopwatch();
            watch.Start();
            var count = await obs.TakeWhile(m => m[0] == "2").Count();
            watch.Stop();
            var frequency = Stopwatch.Frequency * (count + 1) / watch.ElapsedTicks;
            Logger.LogInformation($"Received {frequency:N0} messages/second.");

            var ms = new MemoryStream();
            for (var i = 0; i < 500_000; i++)
                new RequestMessage(ms.Write, Limiter)
                .Write("2", "3", 1, TickType.LastSize, 300)
                .Send();

            // message to indicate test stop
            new RequestMessage(ms.Write, Limiter)
                .Write("1", "3", 1, TickType.LastPrice, 100, 200, true)
                .Send();

            Logger.LogInformation("Sending messages...");
            accept.Send(ms.ToArray(), 0, (int)ms.Position);

            // wait for OnCompleted()
            await obs.LastOrDefaultAsync();

            Logger.LogInformation("Disconnecting.");
            server.Dispose();
            Logger.LogInformation("Disconnected.");
        }
    }
}
