using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nimbus_Internet_Blocker.Services;

public sealed record SnackMessage(string Kind, string Title, string Message);

public interface ISnackbarService
{
    IReadOnlyList<SnackMessage> Messages { get; }
    event Action? OnChange;

    void Info(string title, string message);
    void Success(string title, string message);
    void Warn(string title, string message);
    void Error(string title, string message);
    void Clear();
}

public sealed class SnackbarService : ISnackbarService
{
    private readonly List<SnackMessage> _messages = new();
    public IReadOnlyList<SnackMessage> Messages => _messages;

    public event Action? OnChange;

    private void Push(string kind, string title, string message)
    {
        _messages.Insert(0, new SnackMessage(kind, title, message));
        if (_messages.Count > 4) _messages.RemoveAt(_messages.Count - 1); // cap
        OnChange?.Invoke();
    }

    public void Info(string title, string message) => Push("info", title, message);
    public void Success(string title, string message) => Push("success", title, message);
    public void Warn(string title, string message) => Push("warn", title, message);
    public void Error(string title, string message) => Push("error", title, message);
    public void Clear() { _messages.Clear(); OnChange?.Invoke(); }
}
