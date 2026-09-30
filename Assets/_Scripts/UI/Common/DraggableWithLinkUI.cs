using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DraggableWithLinkUI : DraggableUI
{
    LinkHandler[] handlers;
    
    public void Setup()
    {
        handlers = GetComponentsInChildren<LinkHandler>();

        OnDragAction.AddListener(ENrwe);
        OnDragStartAction.AddListener(Eemer);
    }

    public void ENrwe()
    {
        if(handlers.Length > 0)
        {
            if (!handlers[0].enabled) return;
        }

        foreach(LinkHandler h in handlers)
        {
            h.enabled = false;
        }
    }
    public void Eemer()
    {
        if (handlers.Length > 0)
        {
            if (handlers[0].enabled) return;
        }

        foreach (LinkHandler h in handlers)
        {
            h.enabled = true;
        }
    }
}
