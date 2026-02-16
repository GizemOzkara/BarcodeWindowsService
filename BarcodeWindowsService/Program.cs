using System;
using NLog;

class Program
{
    [Obsolete]
    static void Main(string[] args)
    {
        // NLog config'i ZORLA yükle
        _ = LogManager.LoadConfiguration("nlog.config");
        LogManager.Setup().LoadConfigurationFromFile("nlog.config");
        var logger = LogManager.GetCurrentClassLogger();
        logger.Info("NLOG TEST - PROGRAM START");


        var worker = new BarcodeWorker();
        worker.Start();

        Console.WriteLine("Çalışıyor... Çıkmak için ENTER");
        Console.ReadLine();

        worker.Stop();
        LogManager.Shutdown();
    }
}
