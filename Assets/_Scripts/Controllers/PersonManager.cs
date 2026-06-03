using System.Collections.Generic;
using UnityEngine;

public class PersonManager : MonoBehaviour
{
    public static PersonManager Instance { get; private set; }

    /// <summary>All registered people (runtime registry — not serialized directly).</summary>
    public List<Person> People = new List<Person>();

    private readonly Dictionary<int, Person> _byId = new Dictionary<int, Person>();

    public void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    /// <summary>
    /// Registers a newly-created person, allocating a fresh PersonID.
    /// Returns the allocated ID (also assigned onto the person).
    /// </summary>
    public int RegisterPerson(Person person)
    {
        person.PersonID = IdManager.Instance.AllocatePersonId();
        Index(person);
        return person.PersonID;
    }

    /// <summary>
    /// Indexes an already-IDed person (e.g. one rebuilt during save restore).
    /// Keeps the existing PersonID. Idempotent.
    /// </summary>
    public void RegisterExisting(Person person)
    {
        Index(person);
    }

    private void Index(Person person)
    {
        if (person == null) return;
        if (!_byId.ContainsKey(person.PersonID))
        {
            People.Add(person);
        }
        _byId[person.PersonID] = person;
    }

    public void Unregister(int id)
    {
        if (_byId.TryGetValue(id, out Person person))
        {
            _byId.Remove(id);
            People.Remove(person);
        }
    }

    /// <summary>Clears the registry — called before a save restore.</summary>
    public void Clear()
    {
        People.Clear();
        _byId.Clear();
    }

    public Person GetPerson(int id)
    {
        return _byId.TryGetValue(id, out Person p) ? p : null;
    }

    public Player GetPlayer(int id)
    {
        if (GetPerson(id) is Player player) return player;
        Debug.LogError($"Person with ID {id} isn't a Player (or doesn't exist)");
        return null;
    }

    public Manager GetManager(int id)
    {
        if (GetPerson(id) is Manager manager) return manager;
        Debug.LogError($"Person with ID {id} isn't a Manager (or doesn't exist)");
        return null;
    }
}
