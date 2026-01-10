// Test file for vi( command
public class TestClass
{
    public void TestMethod(int param1, string param2)
    {
        Console.WriteLine("Test content inside parentheses");

        // Test with nested parentheses
        var result = Math.Max(param1, (param2.Length * 2));

        if (result > 0)
        {
            System.Diagnostics.Debug.WriteLine($"Result: {result}");
        }
    }
}