using System;
using System.Collections;
using log4net;


namespace FFPPServer
{
    /// <summary>
    /// Summary description for MessageQueue.
    /// </summary>
    public class MessageQueue
    {
        #region Private Data Members
        private static readonly ILog Log = LogManager.GetLogger(typeof(MessageQueue));

        private readonly Queue _queue;
        #endregion

        #region Constructors and Destructors
        public MessageQueue()
        {
            // Create a synchronized _queue, row.e., one that is thread safe.
            _queue = Queue.Synchronized(new Queue());
        }
        #endregion

        #region Public Methods

        public Message Dequeue()
        {
            Log.Debug("Entering MessageQueue.Dequeue");
            Message result = null;
            if (_queue.Count > 0)
            {
                Object obj = _queue.Dequeue();
                if (obj != null)
                    result = obj as Message;
            }
            Log.Debug("Leaving MessageQueue.Dequeue");
            return result;
        }

        public void Enqueue(Message r)
        {
            Log.Debug("Entering MessageQueue.Enqueue");
            _queue.Enqueue(r);
            Log.Debug("Leaving MessageQueue.Enqueue");
        }

        public int Count => _queue.Count;

        #endregion

    }
}
