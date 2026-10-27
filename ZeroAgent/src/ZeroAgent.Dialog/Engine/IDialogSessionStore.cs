using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using ZeroAgent.Dialog.DST;

namespace ZeroAgent.Dialog.Engine
{
    /// <summary>
    /// Pluggable checkpoint storage abstraction for DialogueSession state (LangGraph Checkpoint Pattern).
    /// Decouples conversational state from single-process memory, enabling multi-node clustering,
    /// crash recovery, and stateful time-travel resumption.
    /// </summary>
    public interface IDialogSessionStore
    {
        DialogueSession GetOrCreate(string sessionId);
        bool TryGet(string sessionId, out DialogueSession? session);
        void Save(DialogueSession session);
        bool Remove(string sessionId);
    }

    /// <summary>
    /// High-performance thread-safe in-memory session store.
    /// </summary>
    public sealed class InMemoryDialogSessionStore : IDialogSessionStore
    {
        private readonly ConcurrentDictionary<string, DialogueSession> _sessions =
            new ConcurrentDictionary<string, DialogueSession>(StringComparer.OrdinalIgnoreCase);

        public DialogueSession GetOrCreate(string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) throw new ArgumentNullException(nameof(sessionId));
            return _sessions.GetOrAdd(sessionId, id => new DialogueSession(id));
        }

        public bool TryGet(string sessionId, out DialogueSession? session)
        {
            return _sessions.TryGetValue(sessionId, out session);
        }

        public void Save(DialogueSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            _sessions[session.SessionId] = session;
        }

        public bool Remove(string sessionId)
        {
            return _sessions.TryRemove(sessionId, out _);
        }

        public int Count => _sessions.Count;
    }

    /// <summary>
    /// File-based JSON Checkpointing Session Store.
    /// Writes session state snapshots to disk so active dialogues and collected slots survive process restarts.
    /// </summary>
    public sealed class FileCheckpointerSessionStore : IDialogSessionStore
    {
        private readonly string _storageDir;
        private readonly InMemoryDialogSessionStore _cache = new InMemoryDialogSessionStore();
        private readonly object _ioLock = new object();

        public FileCheckpointerSessionStore(string storageDir)
        {
            _storageDir = storageDir ?? throw new ArgumentNullException(nameof(storageDir));
            if (!Directory.Exists(_storageDir))
            {
                Directory.CreateDirectory(_storageDir);
            }
        }

        public DialogueSession GetOrCreate(string sessionId)
        {
            if (_cache.TryGet(sessionId, out var cached) && cached != null)
            {
                return cached;
            }

            string path = Path.Combine(_storageDir, $"{sessionId}.json");
            lock (_ioLock)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        string json = File.ReadAllText(path);
                        var session = new DialogueSession(sessionId);
                        var data = JsonSerializer.Deserialize<SessionSnapshotModel>(json);
                        if (data != null)
                        {
                            session.State = data.State;
                            session.PendingRequiredSlot = data.PendingRequiredSlot;
                            foreach (var kvp in data.Slots)
                            {
                                session.SetSlot(kvp.Key, kvp.Value);
                            }
                        }
                        _cache.Save(session);
                        return session;
                    }
                    catch
                    {
                        // Fallback on corrupt file
                    }
                }
            }

            var newSession = _cache.GetOrCreate(sessionId);
            Save(newSession);
            return newSession;
        }

        public bool TryGet(string sessionId, out DialogueSession? session)
        {
            return _cache.TryGet(sessionId, out session);
        }

        public void Save(DialogueSession session)
        {
            if (session == null) return;
            _cache.Save(session);

            string path = Path.Combine(_storageDir, $"{session.SessionId}.json");
            var snapshot = new SessionSnapshotModel
            {
                SessionId = session.SessionId,
                State = session.State,
                PendingRequiredSlot = session.PendingRequiredSlot,
                Slots = new System.Collections.Generic.Dictionary<string, string>(session.Slots)
            };

            lock (_ioLock)
            {
                File.WriteAllText(path, JsonSerializer.Serialize(snapshot));
            }
        }

        public bool Remove(string sessionId)
        {
            _cache.Remove(sessionId);
            string path = Path.Combine(_storageDir, $"{sessionId}.json");
            lock (_ioLock)
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    return true;
                }
            }
            return false;
        }

        private sealed class SessionSnapshotModel
        {
            public string SessionId { get; set; } = string.Empty;
            public SessionState State { get; set; }
            public string? PendingRequiredSlot { get; set; }
            public System.Collections.Generic.Dictionary<string, string> Slots { get; set; } = new System.Collections.Generic.Dictionary<string, string>();
        }
    }
}
