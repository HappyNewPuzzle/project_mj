using System; namespace Mojinloop.InputActivity { public interface IGlobalInputActivitySource { event Action ActivityDetected; void StartListening(); void StopListening(); } }
