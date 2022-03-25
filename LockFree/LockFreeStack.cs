using System;
using System.Threading;

namespace LockFree
{
    public class LockFreeStack<T> : IStack<T>
    {
        private readonly object syncObj = new object();
        private Node<T> head;
        
        public void Push(T obj)
        {
            var newHead = new Node<T> { Value = obj };
            Node<T> oldhead;

            do
            {
                oldhead = head;
                newHead.Next = oldhead;
            } while (Interlocked.CompareExchange(ref head, newHead, oldhead) != oldhead);
        }

        public T Pop()
        {
            T value;

            Node<T> oldhead, newHead;

            do
            {
                oldhead = head;
                newHead = oldhead.Next;
            } while (Interlocked.CompareExchange(ref head, newHead, oldhead) != oldhead);

            return oldhead.Value;
        }
    }
}