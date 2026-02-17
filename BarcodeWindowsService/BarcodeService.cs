using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;

namespace BarcodeService
{
    // Stub Bitmap
    internal class Bitmap
    {
        private MemoryStream ms;
        public Bitmap(MemoryStream ms) { this.ms = ms; }
    }

    // Stub Graphics
    internal class Graphics
    {
        internal void DrawImage(Bitmap src, int pad1, int pad2) { /* do nothing */ }
    }

    public class BarcodeWorker
    {
        private Timer _timer;
        private bool _isWorking;
        private ConcurrentQueue<string> _fileQueue = new ConcurrentQueue<string>();
        private const int WorkerCount = 4;

        private readonly string _watchDir = @"C:\DemoSample";
        private readonly string _outputDir = @"C:\DemoOutputs";
        private readonly string _errorDir = @"C:\DemoError";
        private readonly string _logDir = @"C:\BarcodeService\logs";
        private readonly string _logFile = @"C:\BarcodeService\logs\barcode.log";

        public void Start()
        {
            EnsureDirectories();

            _timer = new Timer(2000);
            _timer.AutoReset = false;
            _timer.Elapsed += OnTimerElapsed;
            _timer.Start();

            Log("Worker başladı");
        }

        public void Stop()
        {
            _timer?.Stop();
            _timer?.Dispose();
            Log("Worker durdu");
        }

        private void OnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            if (_isWorking) return;
            _isWorking = true;

            try
            {
                EnqueueFiles();
                ProcessQueue();
            }
            catch (Exception ex)
            {
                Log("GENEL HATA: " + ex.Message);
            }
            finally
            {
                _isWorking = false;
                _timer.Start();
            }
        }

        private void EnqueueFiles()
        {
            foreach (var file in Directory.GetFiles(_watchDir))
            {
                if (file.EndsWith(".processing")) continue;
                if (IsFileLocked(file)) continue;

                string processingFile = file + ".processing";
                File.Move(file, processingFile);
                _fileQueue.Enqueue(processingFile);
                Log("Queue eklendi: " + processingFile);
            }
        }

        private void ProcessQueue()
        {
            List<Task> workers = new List<Task>();
            for (int i = 0; i < WorkerCount; i++)
            {
                workers.Add(Task.Run(() =>
                {
                    while (_fileQueue.TryDequeue(out string file))
                        ProcessSingleFile(file);
                }));
            }
            Task.WaitAll(workers.ToArray());
        }

        private void ProcessSingleFile(string file)
        {
            try
            {
                Log("İşleniyor: " + file);
                List<string> barcodes = ReadBarcodes(file);

                string originalExt = Path.GetExtension(file.Replace(".processing", ""));

                if (barcodes.Count == 0)
                {
                    string errorPath = Path.Combine(_errorDir, Path.GetFileNameWithoutExtension(file) + originalExt);
                    File.Move(file, errorPath);
                    Log("Barkod yok → error");
                    return;
                }

                int index = 1;
                foreach (var bc in barcodes.Distinct())
                {
                    string safeBarcode = MakeSafeFileName(bc);
                    string newFileName = $"{safeBarcode}_{index}{originalExt}";
                    string targetPath = Path.Combine(_outputDir, newFileName);

                    File.Copy(file, targetPath, true);
                    Log($"Barkod kaydedildi: {safeBarcode}");
                    index++;
                }

                File.Delete(file);
                Log("Processing dosyası silindi");
            }
            catch (Exception ex)
            {
                Log("Dosya işleme hatası: " + ex.Message);
            }
        }

        private List<string> ReadBarcodes(string file)
        {
            string ext = Path.GetExtension(file).ToLower();

            // Test amaçlı dummy barkod döndürüyoruz
            if (ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".pdf")
            {
                // Örnek: dosya adına göre barkod oluştur
                string name = Path.GetFileNameWithoutExtension(file);
                return new List<string> { name + "_BC1", name + "_BC2" };
            }

            Log("Desteklenmeyen uzantı: " + file);
            return new List<string>();
        }

        private bool IsFileLocked(string path)
        {
            try
            {
                using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None)) { }
                return false;
            }
            catch { return true; }
        }

        private string MakeSafeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        private void EnsureDirectories()
        {
            Directory.CreateDirectory(_watchDir);
            Directory.CreateDirectory(_outputDir);
            Directory.CreateDirectory(_errorDir);
            Directory.CreateDirectory(_logDir);
        }

        private void Log(string msg)
        {
            string logMsg = DateTime.Now + " - " + msg;
            File.AppendAllText(_logFile, logMsg + Environment.NewLine);
            Console.WriteLine(logMsg);
        }
    }
}
