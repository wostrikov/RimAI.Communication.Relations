namespace Ustas.RimAI.Communication.Relations.Serialization
{
    /// <summary>
    /// Dependencies: ReflectionJsonFieldSerializer and ReflectionJsonFieldDeserializer.
    /// Responsibility: the one way this module turns an object into JSON and back.
    ///
    /// The contract is the one the module's files and its data classes were
    /// written against: public instance fields and nothing else - no
    /// properties, no maps - with a field the JSON does not mention, or
    /// mentions as null, left as the constructor made it.
    ///
    /// The engine has a JsonUtility of its own and this deliberately does not
    /// ask it. What a serializer does with a property, a null or a number
    /// written as text is the serializer's own business, and an engine is free
    /// to change serializers: RimWorld 1.7 did. A prompt preset that reads
    /// differently after a game update is a preset the player has lost, so the
    /// rules live here, where they are tested.
    /// </summary>
    internal static class RelationsJson
    {
        /// <summary>
        /// Null for an empty text; FormatException for text that is not JSON,
        /// so a caller can tell a broken file from an absent one.
        /// </summary>
        public static T FromJson<T>(string json) where T : class
        {
            return string.IsNullOrWhiteSpace(json) ? null : ReflectionJsonFieldDeserializer.Deserialize<T>(json);
        }

        public static string ToJson(object value, bool prettyPrint = false)
        {
            return value == null ? string.Empty : ReflectionJsonFieldSerializer.Serialize(value, prettyPrint);
        }
    }
}
