using System;
using UnityEngine;

public class FixtureEventArguments : EventArgs
{
    public string FixtureId { get; }
    public FixtureZone FixtureZone { get; }
    public ActionRequester Requester { get; }
    
    // Поля для вложенной оснастки (могут быть null)
    public GameObject ParentObject { get; }
    public string InternalPointName { get; }

    // Конструктор для ОСНОВНОЙ оснастки
    public FixtureEventArguments(object sender, string fixtureId, FixtureZone fixtureZone, ActionRequester requester) : base(sender)
    {
        FixtureId = fixtureId;
        FixtureZone = fixtureZone;
        Requester = requester;
        ParentObject = null;
        InternalPointName = null;
    }

    // Конструктор для ВЛОЖЕННОЙ оснастки
    public FixtureEventArguments(object sender, string fixtureId, FixtureZone fixtureZone, GameObject parentObject, string internalPointName, ActionRequester requester) : base(sender)
    {
        FixtureId = fixtureId;
        FixtureZone = fixtureZone;
        Requester = requester;
        ParentObject = parentObject;
        InternalPointName = internalPointName;
    }

    // Вспомогательный метод для проверки
    public bool IsMainFixture()
    {
        return ParentObject == null && string.IsNullOrEmpty(InternalPointName);
    }
}
