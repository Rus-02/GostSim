using System;

public class SubscriberInfo
{
    public object Subscriber { get; private set; }
    public Action<EventArgs> Listener { get; private set; }

    public SubscriberInfo(object subscriber, Action<EventArgs> listener)
    {
        Subscriber = subscriber;
        Listener = listener;
    }
}