using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PersonManager : MonoBehaviour
{
    public static PersonManager Instance { get; private set; }

    public List<Person> People = new List<Person>();

    int nextPersonID;

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

    public int RegisterPerson(Person person)
    {
        People.Add(person);
        return People.Count;
    }
    public Person GetPerson(int id)
    {
        Person p = People.FirstOrDefault(x => x.PersonID == id);
        return p;
    }
    public Player GetPlayer(int id)
    {
        Person p = People.FirstOrDefault(x => x.PersonID == id);
        if (p.GetType() == typeof(Player)) return (Player)p;
        Debug.LogError($"Person with ID {id} isn't a Player");
        return null;
    }
    public Manager GetManager(int id)
    {
        Person p = People.FirstOrDefault(x => x.PersonID == id);
        if (p.GetType() == typeof(Manager)) return (Manager)p;
        Debug.LogError($"Person with ID {id} isn't a Manager");
        return null;
    }
}
