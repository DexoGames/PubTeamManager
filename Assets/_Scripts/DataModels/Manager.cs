using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

[System.Serializable]
public class Manager : Person
{
    [System.Serializable]
    public struct Stats
    {
        public Skills Skills;
        public Formation Formation;
        public TacticInstruction[] Instructions;
        public TacticTemplate Template;

        public int Tactics => (int)Game.Average(Skills.Intelligence, Skills.Intelligence, Skills.Teamwork);
        public int Motivation => (int)Game.Average(Skills.Agression, Skills.Resilience, Skills.Teamwork);
        public int Communication => (int)Game.Average(Skills.Teamwork, Skills.Teamwork, Skills.Composure, Skills.Intelligence);

    }

    [System.Serializable]
    public struct Skills
    {
        public int Intelligence, Teamwork, Composure, Agression, Resilience;
    }


    public Stats ManStats;

    public int TacticsMatch(Formation mine, Formation other)
    {
        int Tactics = ManStats.Tactics;

        if (other.Inferiors.Contains(mine))
        {
            Tactics = (int)(Tactics * 0.85f);
        }

        return Tactics;
    }


    public Manager(Team team)
    {
        GeneratePerson();

        Team = team;
        Formation[] forms = Resources.LoadAll<Formation>("Formations/Usable");
        ManStats.Formation = forms[Random.Range(0, forms.Length)];

        TacticTemplate[] tacs = Resources.LoadAll<TacticTemplate>("Tactics/Templates");
        ManStats.Template = tacs[Random.Range(0, tacs.Length)];
        ManStats.Instructions = ManStats.Template.Instructions;

        foreach (FieldInfo field in typeof(Skills).GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (field.FieldType == typeof(int))
            {
                field.SetValueDirect(__makeref(ManStats.Skills), UnityEngine.Random.Range(16, 85));
            }
        }
    }

    public static Skills PersonalityModifier(Skills manager, PersonalityType personality)
    {
        int bigChange = 15;
        //int smallChange = 10;

        switch (personality)
        {
            case PersonalityType.Aggressive:
                manager.Agression += bigChange;
                return manager;

            case PersonalityType.Calm:
                manager.Composure += bigChange;
                return manager;

            case PersonalityType.Cautious:
                manager.Composure -= bigChange;
                return manager;

            case PersonalityType.Cocky:
                manager.Teamwork -= bigChange;
                return manager;

            case PersonalityType.Driven:
                manager.Resilience += bigChange;
                return manager;

            case PersonalityType.Kind:
                manager.Agression -= bigChange;
                return manager;

            case PersonalityType.Lazy:
                manager.Intelligence -= bigChange;
                return manager;

            case PersonalityType.Shy:
                manager.Resilience -= bigChange;
                return manager;

            case PersonalityType.Silly:
                manager.Teamwork += bigChange;
                return manager;

            case PersonalityType.Smart:
                manager.Intelligence += bigChange;
                return manager;

            default:
                return manager;
        }
    }
}
