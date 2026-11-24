using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WingspanAccessInstaller.Tests;

/// <summary>One part of a multipart/form-data request.</summary>
internal sealed class FormPart
{
    public string Name { get; set; }
    public string FileName { get; set; }
    public string ContentType { get; set; }
    public byte[] Data { get; set; }
    public string Text => Encoding.UTF8.GetString(Data);
}

internal sealed class RecordedRequest
{
    public string Method { get; set; }
    public string Target { get; set; }
    public Dictionary<string, string> Headers { get; } = new(StringComparer.OrdinalIgnoreCase);
    public byte[] Body { get; set; }
    public List<FormPart> Parts { get; } = new();

    public FormPart Part(string name) => Parts.Find(part => part.Name == name);
}

/// <summary>
/// A stand-in for Static Forms on 127.0.0.1: a minimal HTTP/1.1 server (no administrator rights
/// needed, unlike HttpListener) that records each request and answers what the test says.
/// </summary>
internal sealed class MockStaticForms : IDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly Func<RecordedRequest, (int Status, string Body)> _answer;
    private readonly Task _loop;
    private volatile bool _stopped;

    public MockStaticForms(Func<RecordedRequest, (int Status, string Body)> answer)
    {
        _answer = answer;
        _listener.Start();
        _loop = Task.Run(Loop);
    }

    public string BaseUrl => $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}";

    public List<RecordedRequest> Requests { get; } = new();

    public void Dispose()
    {
        _stopped = true;
        _listener.Stop();
        try
        {
            _loop.Wait(2000);
        }
        catch (AggregateException)
        {
        }
    }

    private async Task Loop()
    {
        while (!_stopped)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync();
            }
            catch (Exception) when (_stopped)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            using (client)
            {
                try
                {
                    Handle(client.GetStream());
                }
                catch (IOException)
                {
                }
            }
        }
    }

    private void Handle(NetworkStream stream)
    {
        stream.ReadTimeout = 10000;
        byte[] head = ReadHead(stream);
        string[] lines = Encoding.ASCII.GetString(head).Split(new[] { "\r\n" }, StringSplitOptions.None);
        string[] start = lines[0].Split(' ');
        var request = new RecordedRequest { Method = start[0], Target = start[1] };
        for (int i = 1; i < lines.Length && lines[i].Length > 0; i++)
        {
            int colon = lines[i].IndexOf(':');
            if (colon > 0)
                request.Headers[lines[i].Substring(0, colon).Trim()] = lines[i].Substring(colon + 1).Trim();
        }

        if (request.Headers.TryGetValue("Expect", out string expect) && expect.Equals("100-continue", StringComparison.OrdinalIgnoreCase))
        {
            byte[] go = Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\n\r\n");
            stream.Write(go, 0, go.Length);
        }

        int length = request.Headers.TryGetValue("Content-Length", out string len) ? int.Parse(len) : 0;
        request.Body = ReadExactly(stream, length);
        if (request.Headers.TryGetValue("Content-Type", out string type) && type.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase))
            ParseMultipart(request, type);
        lock (Requests)
            Requests.Add(request);

        (int status, string body) = _answer(request);
        byte[] content = Encoding.UTF8.GetBytes(body ?? string.Empty);
        string reason = status == 200 ? "OK" : "Error";
        byte[] response = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 {status} {reason}\r\nContent-Type: application/json; charset=utf-8\r\nContent-Length: {content.Length}\r\nConnection: close\r\n\r\n");
        stream.Write(response, 0, response.Length);
        stream.Write(content, 0, content.Length);
        stream.Flush();
    }

    private static byte[] ReadHead(Stream stream)
    {
        var head = new MemoryStream();
        int matched = 0;
        while (matched < 4)
        {
            int b = stream.ReadByte();
            if (b < 0)
                throw new IOException("connection closed in the headers");
            head.WriteByte((byte)b);
            matched = (b == '\r' && matched % 2 == 0) || (b == '\n' && matched % 2 == 1) ? matched + 1 : (b == '\r' ? 1 : 0);
        }

        return head.ToArray();
    }

    private static byte[] ReadExactly(Stream stream, int count)
    {
        var data = new byte[count];
        int done = 0;
        while (done < count)
        {
            int read = stream.Read(data, done, count - done);
            if (read <= 0)
                throw new IOException("connection closed in the body");
            done += read;
        }

        return data;
    }

    /// <summary>Splits the body at the boundary; Latin-1 keeps every byte as one character.</summary>
    private static void ParseMultipart(RecordedRequest request, string contentType)
    {
        string boundary = contentType.Substring(contentType.IndexOf("boundary=", StringComparison.OrdinalIgnoreCase) + 9).Trim('"');
        Encoding latin1 = Encoding.GetEncoding(28591);
        string body = latin1.GetString(request.Body);
        string[] chunks = body.Split(new[] { "--" + boundary }, StringSplitOptions.None);
        foreach (string chunk in chunks)
        {
            if (chunk.StartsWith("--") || chunk.Trim().Length == 0)
                continue;
            int split = chunk.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            string headers = chunk.Substring(0, split);
            string data = chunk.Substring(split + 4);
            if (data.EndsWith("\r\n"))
                data = data.Substring(0, data.Length - 2);
            var part = new FormPart { Data = latin1.GetBytes(data) };
            foreach (string header in headers.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (header.StartsWith("Content-Disposition:", StringComparison.OrdinalIgnoreCase))
                {
                    part.Name = Attribute(header, "name");
                    part.FileName = Attribute(header, "filename");
                }
                else if (header.StartsWith("Content-Type:", StringComparison.OrdinalIgnoreCase))
                {
                    part.ContentType = header.Substring(13).Trim();
                }
            }

            request.Parts.Add(part);
        }
    }

    private static string Attribute(string header, string name)
    {
        foreach (string piece in header.Split(';'))
        {
            string p = piece.Trim();
            if (p.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
                return p.Substring(name.Length + 1).Trim('"');
        }

        return null;
    }
}
