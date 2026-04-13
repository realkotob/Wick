#nullable disable
using System;
using System.Threading.Tasks;
using Wick.Providers.CSharp;
using System.Threading;

sealed class Program
{
    static async Task Main()
    {
        Console.WriteLine("Testing C# LSP Connection (csharp-ls)...");
        try
        {
            var lsp = new CSharpLspClient();
            using var cts = new CancellationTokenSource(60000);

            // Update these paths to match your local checkout
            var slnPath = Path.GetFullPath("../../Wick.sln");
            bool connected = await lsp.EnsureConnectedAsync(slnPath, cts.Token);
            Console.WriteLine("Connected: " + connected);

            if (connected)
            {
                await Task.Delay(2000, cts.Token);

                var csFile = Path.GetFullPath("../../src/Wick.Providers.CSharp/CSharpLspClient.cs");

                var hover = await lsp.GetHoverAsync(csFile, 12, 30, cts.Token);
                Console.WriteLine("Hover Result: " + hover);

                var symbols = await lsp.GetDocumentSymbolsAsync(csFile, cts.Token);
                Console.WriteLine("Symbols Result: " + symbols);
            }
            lsp.Disconnect();
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
        }
    }
}
