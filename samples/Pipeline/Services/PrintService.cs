namespace Shiron.Lib.Samples.Pipeline.Services;

public interface IPrintService {
    void Print(string message);
}

public class PrintService : IPrintService {
    public void Print(string message) {
        Console.WriteLine(message);
    }
}
