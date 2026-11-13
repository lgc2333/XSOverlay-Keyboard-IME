namespace XSOverlayKeyboardIme.Takeover
{
    /// <summary>Immutable page-event identity; never refreshed when queued work runs. See ADR 0060.</summary>
    internal readonly struct FocusTicket
    {
        internal FocusTicket(string document, long revision, long request = 0)
        {
            Document = document;
            Revision = revision;
            Request = request;
        }

        internal string Document { get; }
        internal long Revision { get; }
        internal long Request { get; }

        internal bool SameRevision(FocusTicket other) => Document == other.Document && Revision == other.Revision;

        internal bool SameRequest(FocusTicket other) => SameRevision(other) && Request == other.Request;
    }

    /// <summary>State-only admission shared by Unity and the STA. Never calls external code under its lock.</summary>
    internal sealed class FocusAdmission
    {
        private enum Phase
        {
            Detached,
            Clearing,
            Open,
            Acquiring,
            Held,
        }

        private readonly object _sync = new object();
        private Phase _phase;
        private FocusTicket _ticket;

        internal FocusTicket Bind(string document)
        {
            lock (_sync)
            {
                _ticket = new FocusTicket(document, 1);
                _phase = Phase.Clearing;
                return _ticket;
            }
        }

        internal void Detach()
        {
            lock (_sync)
            {
                _phase = Phase.Detached;
                _ticket = default;
            }
        }

        internal bool Acknowledge(FocusTicket ticket)
        {
            lock (_sync)
            {
                if (_phase != Phase.Clearing || !_ticket.SameRevision(ticket))
                    return false;
                _phase = Phase.Open;
                return true;
            }
        }

        internal bool TryAcquire(FocusTicket ticket)
        {
            lock (_sync)
            {
                if (_phase != Phase.Open || ticket.Request <= 0 || !_ticket.SameRevision(ticket))
                    return false;
                _ticket = ticket;
                _phase = Phase.Acquiring;
                return true;
            }
        }

        internal bool Complete(FocusTicket ticket)
        {
            lock (_sync)
            {
                if (_phase != Phase.Acquiring || !_ticket.SameRequest(ticket))
                    return false;
                _phase = Phase.Held;
                return true;
            }
        }

        internal bool IsAcquiring(FocusTicket ticket)
        {
            lock (_sync)
                return _phase == Phase.Acquiring && _ticket.SameRequest(ticket);
        }

        internal bool Allows(FocusTicket ticket)
        {
            lock (_sync)
                return _phase == Phase.Held && _ticket.SameRequest(ticket);
        }

        internal FocusTicket? Current
        {
            get
            {
                lock (_sync)
                    return _phase == Phase.Held ? _ticket : (FocusTicket?)null;
            }
        }

        internal bool IsClearing(FocusTicket ticket)
        {
            lock (_sync)
                return _phase == Phase.Clearing && _ticket.SameRevision(ticket);
        }

        internal bool Detached
        {
            get
            {
                lock (_sync)
                    return _phase == Phase.Detached;
            }
        }
        internal FocusTicket? PendingClear
        {
            get
            {
                lock (_sync)
                    return _phase == Phase.Clearing ? _ticket : (FocusTicket?)null;
            }
        }

        internal bool Revoke(out FocusTicket clear)
        {
            lock (_sync)
                return RevokeLocked(out clear);
        }

        internal bool Revoke(FocusTicket ticket, out FocusTicket clear)
        {
            lock (_sync)
            {
                if (_phase != Phase.Held || !_ticket.SameRequest(ticket))
                {
                    clear = default;
                    return false;
                }

                return RevokeLocked(out clear);
            }
        }

        internal bool Cancel(FocusTicket ticket, out FocusTicket clear)
        {
            lock (_sync)
            {
                clear = default;
                if (!_ticket.SameRevision(ticket) || ticket.Request <= 0)
                    return false;
                if (_phase != Phase.Open && !_ticket.SameRequest(ticket))
                    return false;
                return RevokeLocked(out clear);
            }
        }

        private bool RevokeLocked(out FocusTicket clear)
        {
            clear = default;
            if (_phase == Phase.Detached || _phase == Phase.Clearing)
                return false;
            _ticket = new FocusTicket(_ticket.Document, _ticket.Revision + 1);
            _phase = Phase.Clearing;
            clear = _ticket;
            return true;
        }
    }
}
