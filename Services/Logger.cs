namespace release_builder.Services;

public static class ConsoleLogger
{
    public static void Info(string message)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write("[INFO] ");
        Console.ResetColor();
        Console.WriteLine(message);
    }

    public static void Success(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("[OK]   ");
        Console.ResetColor();
        Console.WriteLine(message);
    }

    public static void Warning(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("[WARN] ");
        Console.ResetColor();
        Console.WriteLine(message);
    }

    public static void Error(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Write("[FAIL] ");
        Console.ResetColor();
        Console.WriteLine(message);
    }

    public static void Header(string message)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine(new string('═', 60));
        Console.WriteLine($"  {message}");
        Console.WriteLine(new string('═', 60));
        Console.ResetColor();
    }

    public static void SubHeader(string message)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"── {message} ──");
        Console.ResetColor();
    }
}