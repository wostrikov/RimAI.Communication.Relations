using System;
using System.Collections.Generic;
using Ustas.RimAI.Communication.Relations.Serialization;

/// <summary>
/// The contract RelationsJson keeps, which is the one every data class in the
/// module was written against: public fields only, and a field is replaced
/// only by something that was actually read.
/// </summary>
internal static class RelationsJsonTests
{
    private enum Mood
    {
        Calm = 0,
        Angry = 2,
    }

    private sealed class Inner
    {
        public string Name = "unnamed";
        public float Weight;
    }

    private sealed class Sample
    {
        public string Title = "default title";
        public string Note;
        public int Count = 7;
        public bool Enabled;
        public Mood Mood;
        public List<string> Tags = new List<string>();
        public Inner[] Parts;
        public Inner Lead = new Inner();
        public HashSet<string> Deleted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> Lookup = new Dictionary<string, string> { { "kept", "yes" } };
        public string Computed => Title + "!";
        public string Settable { get; set; } = "property";
    }

    public static void Run(Action<bool, string> check)
    {
        RoundTrip(check);
        FieldsOnly(check);
        AbsentAndNullLeaveTheConstructorValue(check);
        Collections(check);
        Lenient(check);
        Failures(check);
        DeepNesting(check);
    }

    static void RoundTrip(Action<bool, string> check)
    {
        var source = new Sample
        {
            Title = "Привіт \"світ\"\n",
            Note = "n",
            Count = -3,
            Enabled = true,
            Mood = Mood.Angry,
            Tags = new List<string> { "a", "b" },
            Parts = new[] { new Inner { Name = "p", Weight = 0.25f } },
            Lead = new Inner { Name = "lead", Weight = 2f },
        };
        Sample back = RelationsJson.FromJson<Sample>(RelationsJson.ToJson(source));
        check(back.Title == source.Title, "json: text with quotes, a line break and Cyrillic survives");
        check(back.Count == -3 && back.Enabled && back.Mood == Mood.Angry, "json: number, flag and enum survive");
        check(back.Tags.Count == 2 && back.Tags[1] == "b", "json: a list survives");
        check(back.Parts.Length == 1 && back.Parts[0].Name == "p" && back.Parts[0].Weight == 0.25f, "json: an array of objects survives");
        check(back.Lead.Name == "lead" && back.Lead.Weight == 2f, "json: a nested object survives");
        check(RelationsJson.ToJson(source, prettyPrint: true).Contains("\n"), "json: pretty printing breaks lines");
        check(!RelationsJson.ToJson(source).Contains("\n\""), "json: compact output is one line");
    }

    static void FieldsOnly(Action<bool, string> check)
    {
        string json = RelationsJson.ToJson(new Sample());
        check(!json.Contains("Computed") && !json.Contains("Settable"), "json: a property is not written");
        check(!json.Contains("Lookup"), "json: a map is not written");
        Sample back = RelationsJson.FromJson<Sample>("{\"Settable\":\"changed\",\"Title\":\"t\"}");
        check(back.Settable == "property" && back.Title == "t", "json: a property is not read");
    }

    static void AbsentAndNullLeaveTheConstructorValue(Action<bool, string> check)
    {
        Sample absent = RelationsJson.FromJson<Sample>("{\"Count\":1}");
        check(absent.Title == "default title" && absent.Tags != null && absent.Lead != null, "json: an absent field keeps what the constructor set");
        Sample nulled = RelationsJson.FromJson<Sample>("{\"Title\":null,\"Tags\":null,\"Lead\":null,\"Count\":2}");
        check(nulled.Title == "default title", "json: a null text leaves the constructor's text");
        check(nulled.Tags != null && nulled.Lead != null && nulled.Count == 2, "json: a null list or object leaves the constructor's");
        Sample map = RelationsJson.FromJson<Sample>("{\"Lookup\":{\"other\":\"x\"}}");
        check(map.Lookup.Count == 1 && map.Lookup["kept"] == "yes", "json: a map in the text leaves the constructor's map");
    }

    static void Collections(Action<bool, string> check)
    {
        var source = new Sample();
        source.Deleted.Add("Alpha");
        Sample back = RelationsJson.FromJson<Sample>(RelationsJson.ToJson(source));
        check(back.Deleted.Count == 1 && back.Deleted.Contains("ALPHA"), "json: a set is read back into the constructor's set, comparer kept");
        Sample empty = RelationsJson.FromJson<Sample>("{\"Tags\":[],\"Parts\":[]}");
        check(empty.Tags.Count == 0 && empty.Parts.Length == 0, "json: empty collections are empty, not null");
    }

    static void Lenient(Action<bool, string> check)
    {
        Sample back = RelationsJson.FromJson<Sample>("{\"count\":\"12\",\"Enabled\":\"true\",\"Mood\":\"2\",\"Unknown\":{\"x\":1},\"Lead\":{\"Weight\":3}}");
        check(back.Count == 12, "json: a number written as text is read, field name in another case");
        check(back.Enabled && back.Mood == Mood.Angry, "json: a flag and an enum written as text are read");
        check(back.Lead.Weight == 3f && back.Lead.Name == "unnamed", "json: a whole number fills a fractional field");
    }

    static void Failures(Action<bool, string> check)
    {
        check(RelationsJson.FromJson<Sample>(null) == null && RelationsJson.FromJson<Sample>("  ") == null, "json: no text is no object");
        check(RelationsJson.ToJson(null) == string.Empty, "json: no object is no text");
        bool threw = false;
        try
        {
            RelationsJson.FromJson<Sample>("{\"Title\":\"unterminated");
        }
        catch (FormatException)
        {
            threw = true;
        }

        check(threw, "json: text that is not JSON is a FormatException, not an empty object");
    }

    private sealed class Node
    {
        public string Id = "";
        public List<Node> Children = new List<Node>();
    }

    static void DeepNesting(Action<bool, string> check)
    {
        var root = new Node { Id = "0" };
        Node tip = root;
        for (int depth = 1; depth <= 14; depth++)
        {
            var child = new Node { Id = depth.ToString() };
            tip.Children.Add(child);
            tip = child;
        }

        Node back = RelationsJson.FromJson<Node>(RelationsJson.ToJson(root, prettyPrint: true));
        int reached = 0;
        for (Node at = back; at.Children.Count > 0; at = at.Children[0])
        {
            reached++;
        }

        check(reached == 14, "json: fourteen levels of nesting come back whole");
    }
}
