using System;
using System.Collections.Generic;

namespace XSOverlayKeyboardIme.Takeover
{
    /// <summary>Serializes page defaults and text writes; completions return to the owner STA.</summary>
    internal sealed class PageOperationQueue
    {
        private readonly Queue<Action<Action<bool>>> _operations = new Queue<Action<Action<bool>>>();
        private readonly Action<Action> _dispatch;
        private readonly Action _failed;
        private readonly Action _resumed;
        private int _generation;

        internal PageOperationQueue(Action<Action> dispatch, Action failed, Action resumed)
        {
            _dispatch = dispatch;
            _failed = failed;
            _resumed = resumed;
        }

        internal bool Pending { get; private set; }

        internal void Enqueue(Action<Action<bool>> operation)
        {
            _operations.Enqueue(operation);
            Start();
        }

        internal void Cancel()
        {
            _generation++;
            Pending = false;
            _operations.Clear();
        }

        private void Start()
        {
            if (Pending || !_operations.TryDequeue(out var operation))
                return;
            Pending = true;
            int generation = ++_generation;
            Action<bool> complete = success => _dispatch(() => Complete(generation, success));
            try
            {
                operation(complete);
            }
            catch (Exception)
            {
                complete(false);
            }
        }

        private void Complete(int generation, bool success)
        {
            if (!Pending || generation != _generation)
                return;
            Pending = false;
            if (!success)
            {
                Cancel();
                _failed();
                return;
            }
            Start();
            _resumed();
        }
    }
}
