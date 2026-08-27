// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace WorkspacesBrowserSync;

// Native messaging host for the "PowerToys Workspaces – Tab Sync" browser extension.
//
// The browser spawns this process when the extension calls sendNativeMessage. We speak the
// Chromium native-messaging wire protocol over stdio: each message is a little-endian uint32
// byte-length header followed by that many bytes of UTF-8 JSON. We read the tab payload, write
// a normalized handoff file that the Workspaces editor can consume (the open tabs flattened into
// an msedge command line), reply with a small status object, and exit when stdin closes.
//
// IMPORTANT: stdout is the protocol channel. Nothing but framed responses may be written there;
// all diagnostics go to the log file or stderr.
internal static class Program
{
    private const long MaxMessageBytes = 16 * 1024 * 1024;

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // Only schemes that can be meaningfully reopened from a browser command line. Internal pages
    // (edge://, chrome://, the new-tab page, extension pages) are dropped. The extension already
    // filters these out; the host repeats the check so it never trusts an unfiltered caller.
    private static readonly string[] SupportedSchemes = ["http://", "https://", "file:"];

    private static int Main()
    {
        using var stdin = Console.OpenStandardInput();
        using var stdout = Console.OpenStandardOutput();

        try
        {
            while (TryReadMessage(stdin, out var message))
            {
                var response = Handle(message);
                WriteMessage(stdout, response);
            }
        }
        catch (Exception ex)
        {
            Log($"Fatal: {ex}");
            return 1;
        }

        return 0;
    }

    private static bool TryReadMessage(Stream stdin, out byte[] message)
    {
        message = [];

        Span<byte> header = stackalloc byte[4];
        if (!TryReadExact(stdin, header))
        {
            // Clean EOF: the browser closed the port.
            return false;
        }

        uint length = BinaryPrimitives.ReadUInt32LittleEndian(header);
        if (length == 0 || length > MaxMessageBytes)
        {
            Log($"Rejecting message with implausible length {length}.");
            return false;
        }

        var buffer = new byte[length];
        if (!TryReadExact(stdin, buffer))
        {
            Log("Stream ended mid-message.");
            return false;
        }

        message = buffer;
        return true;
    }

    private static bool TryReadExact(Stream stream, Span<byte> destination)
    {
        int read = 0;
        while (read < destination.Length)
        {
            int n = stream.Read(destination[read..]);
            if (n == 0)
            {
                return false;
            }

            read += n;
        }

        return true;
    }

    private static void WriteMessage(Stream stdout, object payload)
    {
        byte[] body = JsonSerializer.SerializeToUtf8Bytes(payload);

        Span<byte> header = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(header, (uint)body.Length);

        stdout.Write(header);
        stdout.Write(body);
        stdout.Flush();
    }

    private static object Handle(byte[] message)
    {
        try
        {
            using var document = JsonDocument.Parse(message);
            var root = document.RootElement;

            string browser = TryGetString(root, "browser") ?? "msedge";
            var urls = ExtractUrls(root);

            if (urls.Count == 0)
            {
                Log("Received a payload with no reopenable tabs.");
                return new { ok = true, received = 0, note = "no reopenable tabs in payload" };
            }

            string commandLineArguments = string.Join(' ', urls.Select(u => $"\"{u}\""));
            string handoffPath = WriteHandoff(root, browser, urls, commandLineArguments);

            Log($"Synced {urls.Count} tab(s) from {browser} -> {handoffPath}");
            return new { ok = true, received = urls.Count, handoff = handoffPath };
        }
        catch (JsonException ex)
        {
            Log($"Malformed payload: {ex.Message}");
            return new { ok = false, error = "malformed payload" };
        }
    }

    private static List<string> ExtractUrls(JsonElement root)
    {
        var urls = new List<string>();
        if (root.TryGetProperty("tabs", out var tabs) && tabs.ValueKind == JsonValueKind.Array)
        {
            foreach (var tab in tabs.EnumerateArray())
            {
                string? url = TryGetString(tab, "url");
                if (!string.IsNullOrWhiteSpace(url) && IsReopenable(url))
                {
                    urls.Add(url);
                }
            }
        }

        return urls;
    }

    private static bool IsReopenable(string url) =>
        SupportedSchemes.Any(scheme => url.StartsWith(scheme, StringComparison.OrdinalIgnoreCase));

    private static string WriteHandoff(JsonElement root, string browser, List<string> urls, string commandLineArguments)
    {
        var handoff = new
        {
            type = "workspaces.tabsync",
            browser,
            capturedAt = TryGetString(root, "capturedAt") ?? DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            commandLineArguments,
            urls,
        };

        string folder = DataFolder();
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "browser-tabsync.json");

        // Atomic-ish replace so a reader never sees a half-written file.
        string temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(handoff, WriteOptions), new UTF8Encoding(false));
        File.Move(temp, path, overwrite: true);

        return path;
    }

    private static string? TryGetString(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string DataFolder() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Microsoft",
        "PowerToys",
        "Workspaces");

    private static void Log(string line)
    {
        try
        {
            string folder = Path.Combine(DataFolder(), "Logs");
            Directory.CreateDirectory(folder);
            string stamp = DateTimeOffset.Now.ToString("u", CultureInfo.InvariantCulture);
            File.AppendAllText(Path.Combine(folder, "browser-sync.log"), $"{stamp}  {line}{Environment.NewLine}");
        }
        catch
        {
            // Logging must never take the host down.
        }
    }
}
