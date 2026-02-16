using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;

public class BarcodeWorker
{
    private System.Timers.Timer timer;
    private bool isWorking = false;

    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

    private readonly ConcurrentQueue<string> fileQueue = new ConcurrentQueue<string>();
    private const int WorkerCount = 4;

    private readonly string watch = @"C:\DemoSample";
    private readonly string outDir = @"C:\DemoOutputs";
    private readonly string errDir = @"C:\DemoError";

    public void Start()
    {

        logger.Info("ELASTIC TEST LOG - PROGRAM START");
        Directory.CreateDirectory(watch);
        Directory.CreateDirectory(outDir);
        Directory.CreateDirectory(errDir);

        timer = new System.Timers.Timer(2000);
        timer.AutoReset = false;
        timer.Elapsed += Timer_Elapsed;
        timer.Start();

        logger.Info("Worker başladı");
    }

    public void Stop()
    {
        timer?.Stop();
        timer?.Dispose();

        logger.Info("Worker durdu");
    }

    private void Timer_Elapsed(object sender, ElapsedEventArgs e)
    {
        if (isWorking)
            return;

        isWorking = true;

        try
        {
            EnqueueFiles();
            ProcessQueue();
        }
        catch (Exception ex)
        {
            logger.Error(ex, "GENEL HATA");
        }
        finally
        {
            isWorking = false;
            timer.Start();
        }
    }

    private void EnqueueFiles()
    {
        foreach (var file in Directory.GetFiles(watch))
        {
            if (file.EndsWith(".processing"))
                continue;

            try
            {
                string processingFile = file + ".processing";
                File.Move(file, processingFile);

                fileQueue.Enqueue(processingFile);
                logger.Info($"Queue eklendi → {processingFile}");
            }
            catch (Exception ex)
            {
                logger.Warn(ex, $"Queue eklenemedi → {file}");
            }
        }
    }

    private void ProcessQueue()
    {
        List<Task> workers = new List<Task>();

        for (int i = 0; i < WorkerCount; i++)
        {
            workers.Add(Task.Run(() =>
            {
                while (fileQueue.TryDequeue(out string file))
                {
                    ProcessSingleFile(file);
                }
            }));
        }

        Task.WaitAll(workers.ToArray());
    }

    private void ProcessSingleFile(string processingFile)
    {
        try
        {
            logger.Info($"İşleniyor → {processingFile}");

            string originalFile = processingFile.Replace(".processing", "");
            string ext = Path.GetExtension(originalFile).ToLower();

            List<string> barcodes = new List<string>();

            if (ext == ".jpg" || ext == ".jpeg" || ext == ".png")
                barcodes = BarcodeReaderEngine.ReadFromImage(processingFile);
            else if (ext == ".pdf")
                barcodes = BarcodeReaderEngine.ReadFromPdf(processingFile);

            if (barcodes.Any())
            {
                barcodes = barcodes.Distinct().ToList();
                int i = 1;

                foreach (var code in barcodes)
                {
                    string safe = MakeSafeFileName(code);
                    string target = Path.Combine(outDir, $"{safe}_{i}{ext}");
                    File.Copy(processingFile, target, true);

                    logger.Info($"Dosya üretildi → {target}");
                    i++;
                }

                File.Delete(processingFile);
                logger.Info($"İşlem tamamlandı → {processingFile}");
            }
            else
            {
                File.Move(processingFile, Path.Combine(errDir, Path.GetFileName(originalFile)));
                logger.Warn($"Barkod bulunamadı → {processingFile}");
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, $"Dosya HATASI → {processingFile}");
        }
    }

    private string MakeSafeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');

        return name;
    }
}
