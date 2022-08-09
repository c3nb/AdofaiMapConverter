using System;
using System.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

namespace JSON
{
    public enum JsonNodeType
    {
        Array = 1,
        Object = 2,
        String = 3,
        Number = 4,
        NullValue = 5,
        Boolean = 6,
        None = 7,
        Custom = 0xFF,
    }
    public enum JsonTextMode
    {
        Compact,
        Indent
    }

    public abstract partial class JsonNode : IEnumerable<KeyValuePair<string, JsonNode>>
    {
        #region Enumerators
        public class Enumerator : IEnumerable<KeyValuePair<string, JsonNode>>, IEnumerator<KeyValuePair<string, JsonNode>>
        {
            enum Type { None, Array, Object };
            readonly Type type = Type.None;
            int pos = -1;
            protected List<KeyValuePair<string, JsonNode>> m_Object;
            readonly List<JsonNode> m_Array;
            public bool IsValid => type != Type.None;
            public Enumerator()
            {
                type = Type.None;
                m_Object = default;
                m_Array = default;
            }
            protected Enumerator(Enumerator enumerator)
            {
                type = enumerator.type;
                m_Object = enumerator.m_Object;
                m_Array = enumerator.m_Array;
            }
            public Enumerator(List<JsonNode> aArray)
            {
                type = Type.Array;
                m_Object = default;
                m_Array = aArray;
            }
            public Enumerator(List<KeyValuePair<string, JsonNode>> aList)
            {
                type = Type.Object;
                m_Object = aList;
                m_Array = default;
            }
            public Enumerator(Dictionary<string, JsonNode> aDict)
            {
                type = Type.Object;
                m_Object = aDict.ToList();
                m_Array = default;
            }
            public void Dispose() => GC.SuppressFinalize(this);
            public bool MoveNext()
            {
                if (type == Type.Array) return ++pos < m_Array.Count;
                else if (type == Type.Object) return ++pos < m_Object.Count;
                else return false;
            }
            public void Reset() => pos = -1;
            object IEnumerator.Current => Current;
            public KeyValuePair<string, JsonNode> Current
            {
                get
                {
                    if (type == Type.Array)
                        return new KeyValuePair<string, JsonNode>(string.Empty, m_Array[pos]);
                    else if (type == Type.Object)
                        return m_Object[pos];
                    return new KeyValuePair<string, JsonNode>(string.Empty, null);
                }
            }
            public IEnumerator<KeyValuePair<string, JsonNode>> GetEnumerator() => this;
            IEnumerator IEnumerable.GetEnumerator() => this;
        }
        public class ValueEnumerator : IEnumerable<JsonNode>, IEnumerator<JsonNode>
        {
            public Enumerator enumerator;
            public ValueEnumerator(Enumerator enumerator) { this.enumerator = enumerator; }
            public ValueEnumerator(List<JsonNode> aArray) { enumerator = new Enumerator(aArray); }
            public ValueEnumerator(Dictionary<string, JsonNode> aDict) { enumerator = new Enumerator(aDict); }
            public JsonNode Current => enumerator.Current.Value;
            public bool MoveNext() => enumerator.MoveNext();
            public IEnumerator<JsonNode> GetEnumerator() => this;
            public void Reset() => enumerator.Reset();
            public void Dispose() => enumerator.Dispose();
            IEnumerator IEnumerable.GetEnumerator() => this;
            object IEnumerator.Current => Current;
        }
        public class KeyEnumerator : IEnumerable<string>, IEnumerator<string>
        {
            public Enumerator enumerator;
            public KeyEnumerator(Enumerator enumerator) { this.enumerator = enumerator; }
            public KeyEnumerator(List<JsonNode> aArray) { enumerator = new Enumerator(aArray); }
            public KeyEnumerator(Dictionary<string, JsonNode> aDict) { enumerator = new Enumerator(aDict); }
            public string Current => enumerator.Current.Key;
            public bool MoveNext() => enumerator.MoveNext();
            public IEnumerator<string> GetEnumerator() => this;
            public void Reset() => enumerator.Reset();
            public void Dispose() => enumerator.Dispose();
            IEnumerator IEnumerable.GetEnumerator() => this;
            object IEnumerator.Current => Current;
        }
        public class KeyValueEnumerator : Enumerator
        {
            public KeyValueEnumerator(Enumerator enumerator) : base(enumerator) { }
        }

        #endregion Enumerators

        #region common interface
        public static JsonNode Empty => new JsonObject();
        public static bool forceASCII = false; // Use Unicode by default
        public static bool longAsString = false; // lazy creator creates a JsonString instead of JsonNumber
        public static bool allowLineComments = true; // allow "//"-style comments at the end of a line

        public abstract JsonNodeType Tag { get; }

        public virtual JsonNode this[int aIndex] { get { return null; } set { } }

        public virtual JsonNode this[string aKey] { get { return null; } set { } }

        public virtual string Value { get { return ""; } set { } }

        public virtual int Count { get { return 0; } }

        public virtual bool IsNumber { get { return false; } }
        public virtual bool IsString { get { return false; } }
        public virtual bool IsBoolean { get { return false; } }
        public virtual bool IsNull { get { return false; } }
        public virtual bool IsArray { get { return false; } }
        public virtual bool IsObject { get { return false; } }

        public virtual bool Inline { get { return false; } set { } }

        public virtual void Add(string aKey, JsonNode aItem)
        {
        }
        public virtual void Add(JsonNode aItem)
        {
            Add("", aItem);
        }

        public virtual JsonNode Remove(string aKey)
        {
            return null;
        }

        public virtual JsonNode Remove(int aIndex)
        {
            return null;
        }

        public virtual JsonNode Remove(JsonNode aNode)
        {
            return aNode;
        }
        public virtual void Clear() { }

        public virtual JsonNode Clone()
        {
            return null;
        }

        public virtual IEnumerable<JsonNode> Children
        {
            get
            {
                yield break;
            }
        }

        public IEnumerable<JsonNode> DeepChildren
        {
            get
            {
                foreach (var C in Children)
                    foreach (var D in C.DeepChildren)
                        yield return D;
            }
        }

        public virtual bool HasKey(string aKey)
        {
            return false;
        }

        public virtual JsonNode GetValueOrDefault(string aKey, JsonNode aDefault)
        {
            return aDefault;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            WriteToStringBuilder(sb, 0, 0, JsonTextMode.Compact);
            return sb.ToString();
        }

        public virtual string ToString(int aIndent)
        {
            StringBuilder sb = new StringBuilder();
            WriteToStringBuilder(sb, 0, aIndent, JsonTextMode.Indent);
            return sb.ToString();
        }
        internal abstract void WriteToStringBuilder(StringBuilder aSB, int aIndent, int aIndentInc, JsonTextMode aMode);
        public abstract Enumerator GetEnumerator();
        IEnumerator<KeyValuePair<string, JsonNode>> IEnumerable<KeyValuePair<string, JsonNode>>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public KeyEnumerator Keys => new KeyEnumerator(GetEnumerator());
        public ValueEnumerator Values => new ValueEnumerator(GetEnumerator());
        public KeyValueEnumerator KeyValues => new KeyValueEnumerator(GetEnumerator());

        #endregion common interface

        #region typecasting properties


        public virtual double AsDouble
        {
            get
            {
                if (double.TryParse(Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
                    return v;
                return 0.0;
            }
            set
            {
                Value = value.ToString(CultureInfo.InvariantCulture);
            }
        }

        public virtual int AsInt
        {
            get { return (int)AsDouble; }
            set { AsDouble = value; }
        }

        public virtual float AsFloat
        {
            get { return (float)AsDouble; }
            set { AsDouble = value; }
        }

        public virtual bool AsBool
        {
            get
            {
                if (bool.TryParse(Value, out bool v))
                    return v;
                return !string.IsNullOrEmpty(Value);
            }
            set
            {
                Value = value ? "true" : "false";
            }
        }

        public virtual long AsLong
        {
            get
            {
                if (long.TryParse(Value, out long val))
                    return val;
                return 0L;
            }
            set
            {
                Value = value.ToString();
            }
        }

        public virtual ulong AsULong
        {
            get
            {
                if (ulong.TryParse(Value, out ulong val))
                    return val;
                return 0;
            }
            set
            {
                Value = value.ToString();
            }
        }

        public virtual JsonArray AsArray
        {
            get
            {
                return this as JsonArray;
            }
        }

        public virtual JsonObject AsObject
        {
            get
            {
                return this as JsonObject;
            }
        }


        #endregion typecasting properties

        #region operators

        public static implicit operator JsonNode(string s)
        {
            return (s == null) ? (JsonNode) JsonNull.CreateOrGet() : new JsonString(s);
        }
        public static implicit operator string(JsonNode d)
        {
            return d?.Value;
        }

        public static implicit operator JsonNode(double n)
        {
            return new JsonNumber(n);
        }
        public static implicit operator double(JsonNode d)
        {
            return (d == null) ? 0 : d.AsDouble;
        }

        public static implicit operator JsonNode(float n)
        {
            return new JsonNumber(n);
        }
        public static implicit operator float(JsonNode d)
        {
            return (d == null) ? 0 : d.AsFloat;
        }

        public static implicit operator JsonNode(int n)
        {
            return new JsonNumber(n);
        }
        public static implicit operator int(JsonNode d)
        {
            return (d == null) ? 0 : d.AsInt;
        }

        public static implicit operator JsonNode(long n)
        {
            if (longAsString)
                return new JsonString(n.ToString());
            return new JsonNumber(n);
        }
        public static implicit operator long(JsonNode d)
        {
            return (d == null) ? 0L : d.AsLong;
        }

        public static implicit operator JsonNode(ulong n)
        {
            if (longAsString)
                return new JsonString(n.ToString());
            return new JsonNumber(n);
        }
        public static implicit operator ulong(JsonNode d)
        {
            return (d == null) ? 0 : d.AsULong;
        }

        public static implicit operator JsonNode(bool b)
        {
            return new JsonBool(b);
        }
        public static implicit operator bool(JsonNode d)
        {
            return d != null && d.AsBool;
        }

        public static implicit operator JsonNode(KeyValuePair<string, JsonNode> aKeyValue)
        {
            return aKeyValue.Value;
        }

        public static bool operator ==(JsonNode a, object b)
        {
            if (ReferenceEquals(a, b))
                return true;
            bool aIsNull = a is JsonNull || a is null || a is JsonLazyCreator;
            bool bIsNull = b is JsonNull || b is null || b is JsonLazyCreator;
            if (aIsNull && bIsNull)
                return true;
            return !aIsNull && a.Equals(b);
        }

        public static bool operator !=(JsonNode a, object b)
        {
            return !(a == b);
        }

        public override bool Equals(object obj)
        {
            return ReferenceEquals(this, obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        #endregion operators

        [ThreadStatic]
        private static StringBuilder m_EscapeBuilder;
        internal static StringBuilder EscapeBuilder
        {
            get
            {
                if (m_EscapeBuilder == null)
                    m_EscapeBuilder = new StringBuilder();
                return m_EscapeBuilder;
            }
        }
        internal static string Escape(string aText)
        {
            var sb = EscapeBuilder;
            sb.Length = 0;
            if (sb.Capacity < aText.Length + aText.Length / 10)
                sb.Capacity = aText.Length + aText.Length / 10;
            foreach (char c in aText)
            {
                switch (c)
                {
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '\"':
                        sb.Append("\\\"");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    case '\b':
                        sb.Append("\\b");
                        break;
                    case '\f':
                        sb.Append("\\f");
                        break;
                    default:
                        if (c < ' ' || (forceASCII && c > 127))
                        {
                            ushort val = c;
                            sb.Append("\\u").Append(val.ToString("X4"));
                        }
                        else
                            sb.Append(c);
                        break;
                }
            }
            string result = sb.ToString();
            sb.Length = 0;
            return result;
        }

        private static JsonNode ParseElement(string token, bool quoted)
        {
            if (quoted)
                return token;
            if (token.Length <= 5)
            {
                string tmp = token.ToLower();
                if (tmp == "false" || tmp == "true")
                    return tmp == "true";
                if (tmp == "null")
                    return JsonNull.CreateOrGet();
            }
            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
                return val;
            else
                return token;
        }

        public static JsonNode Parse(string aJson)
        {
            Stack<JsonNode> stack = new Stack<JsonNode>();
            JsonNode ctx = null;
            int i = 0;
            StringBuilder Token = new StringBuilder();
            string TokenName = "";
            bool QuoteMode = false;
            bool TokenIsQuoted = false;
            bool HasNewlineChar = false;
            while (i < aJson.Length)
            {
                switch (aJson[i])
                {
                    case '{':
                        if (QuoteMode)
                        {
                            Token.Append(aJson[i]);
                            break;
                        }
                        stack.Push(new JsonObject());
                        if (ctx != null)
                        {
                            ctx.Add(TokenName, stack.Peek());
                        }
                        TokenName = "";
                        Token.Length = 0;
                        ctx = stack.Peek();
                        HasNewlineChar = false;
                        break;

                    case '[':
                        if (QuoteMode)
                        {
                            Token.Append(aJson[i]);
                            break;
                        }

                        stack.Push(new JsonArray());
                        if (ctx != null)
                        {
                            ctx.Add(TokenName, stack.Peek());
                        }
                        TokenName = "";
                        Token.Length = 0;
                        ctx = stack.Peek();
                        HasNewlineChar = false;
                        break;

                    case '}':
                    case ']':
                        if (QuoteMode)
                        {

                            Token.Append(aJson[i]);
                            break;
                        }
                        if (stack.Count == 0)
                            throw new Exception("Json Parse: Too many closing brackets");

                        stack.Pop();
                        if (Token.Length > 0 || TokenIsQuoted)
                            ctx.Add(TokenName, ParseElement(Token.ToString(), TokenIsQuoted));
                        if (ctx != null)
                            ctx.Inline = !HasNewlineChar;
                        TokenIsQuoted = false;
                        TokenName = "";
                        Token.Length = 0;
                        if (stack.Count > 0)
                            ctx = stack.Peek();
                        break;

                    case ':':
                        if (QuoteMode)
                        {
                            Token.Append(aJson[i]);
                            break;
                        }
                        TokenName = Token.ToString();
                        Token.Length = 0;
                        TokenIsQuoted = false;
                        break;

                    case '"':
                        QuoteMode ^= true;
                        TokenIsQuoted |= QuoteMode;
                        break;

                    case ',':
                        if (QuoteMode)
                        {
                            Token.Append(aJson[i]);
                            break;
                        }
                        if (Token.Length > 0 || TokenIsQuoted)
                            ctx.Add(TokenName, ParseElement(Token.ToString(), TokenIsQuoted));
                        TokenName = "";
                        Token.Length = 0;
                        TokenIsQuoted = false;
                        break;

                    case '\r':
                    case '\n':
                        HasNewlineChar = true;
                        break;

                    case ' ':
                    case '\t':
                        if (QuoteMode)
                            Token.Append(aJson[i]);
                        break;

                    case '\\':
                        ++i;
                        if (QuoteMode)
                        {
                            char C = aJson[i];
                            switch (C)
                            {
                                case 't':
                                    Token.Append('\t');
                                    break;
                                case 'r':
                                    Token.Append('\r');
                                    break;
                                case 'n':
                                    Token.Append('\n');
                                    break;
                                case 'b':
                                    Token.Append('\b');
                                    break;
                                case 'f':
                                    Token.Append('\f');
                                    break;
                                case 'u':
                                    {
                                        string s = aJson.Substring(i + 1, 4);
                                        Token.Append((char)int.Parse(
                                            s,
                                            System.Globalization.NumberStyles.AllowHexSpecifier));
                                        i += 4;
                                        break;
                                    }
                                default:
                                    Token.Append(C);
                                    break;
                            }
                        }
                        break;
                    case '/':
                        if (allowLineComments && !QuoteMode && i + 1 < aJson.Length && aJson[i + 1] == '/')
                        {
                            while (++i < aJson.Length && aJson[i] != '\n' && aJson[i] != '\r') ;
                            break;
                        }
                        Token.Append(aJson[i]);
                        break;
                    case '\uFEFF': // remove / ignore BOM (Byte Order Mark)
                        break;

                    default:
                        Token.Append(aJson[i]);
                        break;
                }
                ++i;
            }
            if (QuoteMode)
            {
                throw new Exception("Json Parse: Quotation marks seems to be messed up.");
            }
            if (ctx == null)
                return ParseElement(Token.ToString(), TokenIsQuoted);
            return ctx;
        }

    }
    // End of JsonNode

    public partial class JsonArray : JsonNode
    {
        private readonly List<JsonNode> m_List = new List<JsonNode>();
        private bool inline = false;
        public override bool Inline
        {
            get { return inline; }
            set { inline = value; }
        }

        public override JsonNodeType Tag { get { return JsonNodeType.Array; } }
        public override bool IsArray { get { return true; } }
        public override Enumerator GetEnumerator() { return new Enumerator(m_List); }

        public override JsonNode this[int aIndex]
        {
            get
            {
                if (aIndex < 0 || aIndex >= m_List.Count)
                    return new JsonLazyCreator(this);
                return m_List[aIndex];
            }
            set
            {
                if (value == null)
                    value = JsonNull.CreateOrGet();
                if (aIndex < 0 || aIndex >= m_List.Count)
                    m_List.Add(value);
                else
                    m_List[aIndex] = value;
            }
        }

        public override JsonNode this[string aKey]
        {
            get { return new JsonLazyCreator(this); }
            set
            {
                if (value == null)
                    value = JsonNull.CreateOrGet();
                m_List.Add(value);
            }
        }

        public override int Count
        {
            get { return m_List.Count; }
        }

        public override void Add(string aKey, JsonNode aItem)
        {
            if (aItem == null)
                aItem = JsonNull.CreateOrGet();
            m_List.Add(aItem);
        }

        public override JsonNode Remove(int aIndex)
        {
            if (aIndex < 0 || aIndex >= m_List.Count)
                return null;
            JsonNode tmp = m_List[aIndex];
            m_List.RemoveAt(aIndex);
            return tmp;
        }

        public override JsonNode Remove(JsonNode aNode)
        {
            m_List.Remove(aNode);
            return aNode;
        }

        public override void Clear()
        {
            m_List.Clear();
        }

        public override JsonNode Clone()
        {
            var node = new JsonArray();
            node.m_List.Capacity = m_List.Capacity;
            foreach(var n in m_List)
            {
                if (n != null)
                    node.Add(n.Clone());
                else
                    node.Add(null);
            }
            return node;
        }

        public override IEnumerable<JsonNode> Children
        {
            get
            {
                foreach (JsonNode N in m_List)
                    yield return N;
            }
        }


        internal override void WriteToStringBuilder(StringBuilder aSB, int aIndent, int aIndentInc, JsonTextMode aMode)
        {
            aSB.Append('[');
            int count = m_List.Count;
            if (inline)
                aMode = JsonTextMode.Compact;
            for (int i = 0; i < count; i++)
            {
                if (i > 0)
                    aSB.Append(", ");
                if (aMode == JsonTextMode.Indent)
                    aSB.AppendLine();

                if (aMode == JsonTextMode.Indent)
                    aSB.Append(' ', aIndent + aIndentInc);
                m_List[i].WriteToStringBuilder(aSB, aIndent + aIndentInc, aIndentInc, aMode);
            }
            if (aMode == JsonTextMode.Indent)
                aSB.AppendLine().Append(' ', aIndent);
            aSB.Append(']');
        }
    }
    // End of JsonArray

    public partial class JsonObject : JsonNode
    {
        internal readonly Dictionary<string, JsonNode> m_Dict = new Dictionary<string, JsonNode>();

        private bool inline = false;
        public override bool Inline
        {
            get { return inline; }
            set { inline = value; }
        }

        public override JsonNodeType Tag { get { return JsonNodeType.Object; } }
        public override bool IsObject { get { return true; } }

        public override Enumerator GetEnumerator() { return new Enumerator(m_Dict); }


        public override JsonNode this[string aKey]
        {
            get
            {
                if (m_Dict.ContainsKey(aKey))
                    return m_Dict[aKey];
                else
                    return new JsonLazyCreator(this, aKey);
            }
            set
            {
                if (value == null)
                    value = JsonNull.CreateOrGet();
                if (m_Dict.ContainsKey(aKey))
                    m_Dict[aKey] = value;
                else
                    m_Dict.Add(aKey, value);
            }
        }

        public override JsonNode this[int aIndex]
        {
            get
            {
                if (aIndex < 0 || aIndex >= m_Dict.Count)
                    return null;
                return m_Dict.ElementAt(aIndex).Value;
            }
            set
            {
                if (value == null)
                    value = JsonNull.CreateOrGet();
                if (aIndex < 0 || aIndex >= m_Dict.Count)
                    return;
                string key = m_Dict.ElementAt(aIndex).Key;
                m_Dict[key] = value;
            }
        }

        public override int Count
        {
            get { return m_Dict.Count; }
        }

        public override void Add(string aKey, JsonNode aItem)
        {
            if (aItem == null)
                aItem = JsonNull.CreateOrGet();

            if (aKey != null)
            {
                if (m_Dict.ContainsKey(aKey))
                    m_Dict[aKey] = aItem;
                else
                    m_Dict.Add(aKey, aItem);
            }
            else
                m_Dict.Add(Guid.NewGuid().ToString(), aItem);
        }

        public override JsonNode Remove(string aKey)
        {
            if (!m_Dict.ContainsKey(aKey))
                return null;
            JsonNode tmp = m_Dict[aKey];
            m_Dict.Remove(aKey);
            return tmp;
        }

        public override JsonNode Remove(int aIndex)
        {
            if (aIndex < 0 || aIndex >= m_Dict.Count)
                return null;
            var item = m_Dict.ElementAt(aIndex);
            m_Dict.Remove(item.Key);
            return item.Value;
        }

        public override JsonNode Remove(JsonNode aNode)
        {
            try
            {
                var item = m_Dict.Where(k => k.Value == aNode).First();
                m_Dict.Remove(item.Key);
                return aNode;
            }
            catch
            {
                return null;
            }
        }

        public override void Clear()
        {
            m_Dict.Clear();
        }

        public override JsonNode Clone()
        {
            var node = new JsonObject();
            foreach (var n in m_Dict)
            {
                node.Add(n.Key, n.Value.Clone());
            }
            return node;
        }

        public override bool HasKey(string aKey)
        {
            return m_Dict.ContainsKey(aKey);
        }

        public override JsonNode GetValueOrDefault(string aKey, JsonNode aDefault)
        {
            if (m_Dict.TryGetValue(aKey, out JsonNode res))
                return res;
            return aDefault;
        }

        public override IEnumerable<JsonNode> Children
        {
            get
            {
                foreach (KeyValuePair<string, JsonNode> N in m_Dict)
                    yield return N.Value;
            }
        }

        internal override void WriteToStringBuilder(StringBuilder aSB, int aIndent, int aIndentInc, JsonTextMode aMode)
        {
            aSB.Append("{ ");
            bool first = true;
            if (inline)
                aMode = JsonTextMode.Compact;
            foreach (var k in m_Dict)
            {
                if (!first)
                    aSB.Append(", ");
                first = false;
                if (aMode == JsonTextMode.Indent)
                    aSB.AppendLine();
                if (aMode == JsonTextMode.Indent)
                    aSB.Append(' ', aIndent + aIndentInc);
                aSB.Append('\"').Append(Escape(k.Key)).Append('\"');
                aSB.Append(": ");
                //if (aMode == JsonTextMode.Compact)
                    //aSB.Append(':');
                //else
                    //aSB.Append(" : ");
                k.Value.WriteToStringBuilder(aSB, aIndent + aIndentInc, aIndentInc, aMode);
            }
            if (aMode == JsonTextMode.Indent)
                aSB.AppendLine().Append(' ', aIndent);
            aSB.Append(" }");
        }

    }
    // End of JsonObject

    public partial class JsonString : JsonNode
    {
        private string m_Data;

        public override JsonNodeType Tag { get { return JsonNodeType.String; } }
        public override bool IsString { get { return true; } }

        public override Enumerator GetEnumerator() { return new Enumerator(); }


        public override string Value
        {
            get { return m_Data; }
            set
            {
                m_Data = value;
            }
        }

        public JsonString(string aData)
        {
            m_Data = aData;
        }
        public override JsonNode Clone()
        {
            return new JsonString(m_Data);
        }

        internal override void WriteToStringBuilder(StringBuilder aSB, int aIndent, int aIndentInc, JsonTextMode aMode)
        {
            aSB.Append('\"').Append(Escape(m_Data)).Append('\"');
        }
        public override bool Equals(object obj)
        {
            if (base.Equals(obj))
                return true;
            if (obj is string s)
                return m_Data == s;
            JsonString s2 = obj as JsonString;
            if (s2 != null)
                return m_Data == s2.m_Data;
            return false;
        }
        public override int GetHashCode()
        {
            return m_Data.GetHashCode();
        }
        public override void Clear()
        {
            m_Data = "";
        }
    }
    // End of JsonString

    public partial class JsonNumber : JsonNode
    {
        private double m_Data;

        public override JsonNodeType Tag { get { return JsonNodeType.Number; } }
        public override bool IsNumber { get { return true; } }
        public override Enumerator GetEnumerator() { return new Enumerator(); }

        public override string Value
        {
            get { return m_Data.ToString(CultureInfo.InvariantCulture); }
            set
            {
                if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
                    m_Data = v;
            }
        }

        public override double AsDouble
        {
            get { return m_Data; }
            set { m_Data = value; }
        }
        public override long AsLong
        {
            get { return (long)m_Data; }
            set { m_Data = value; }
        }
        public override ulong AsULong
        {
            get { return (ulong)m_Data; }
            set { m_Data = value; }
        }

        public JsonNumber(double aData)
        {
            m_Data = aData;
        }

        public JsonNumber(string aData)
        {
            Value = aData;
        }

        public override JsonNode Clone()
        {
            return new JsonNumber(m_Data);
        }

        internal override void WriteToStringBuilder(StringBuilder aSB, int aIndent, int aIndentInc, JsonTextMode aMode)
        {
            aSB.Append(Value);
        }
        private static bool IsNumeric(object value)
        {
            return value is int || value is uint
                || value is float || value is double
                || value is decimal
                || value is long || value is ulong
                || value is short || value is ushort
                || value is sbyte || value is byte;
        }
        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;
            if (base.Equals(obj))
                return true;
            JsonNumber s2 = obj as JsonNumber;
            if (s2 != null)
                return m_Data == s2.m_Data;
            if (IsNumeric(obj))
                return Convert.ToDouble(obj) == m_Data;
            return false;
        }
        public override int GetHashCode()
        {
            return m_Data.GetHashCode();
        }
        public override void Clear()
        {
            m_Data = 0;
        }
    }
    // End of JsonNumber

    public partial class JsonBool : JsonNode
    {
        private bool m_Data;

        public override JsonNodeType Tag { get { return JsonNodeType.Boolean; } }
        public override bool IsBoolean { get { return true; } }
        public override Enumerator GetEnumerator() { return new Enumerator(); }

        public override string Value
        {
            get { return m_Data.ToString(); }
            set
            {
                if (bool.TryParse(value, out bool v))
                    m_Data = v;
            }
        }
        public override bool AsBool
        {
            get { return m_Data; }
            set { m_Data = value; }
        }

        public JsonBool(bool aData)
        {
            m_Data = aData;
        }

        public JsonBool(string aData)
        {
            Value = aData;
        }

        public override JsonNode Clone()
        {
            return new JsonBool(m_Data);
        }

        internal override void WriteToStringBuilder(StringBuilder aSB, int aIndent, int aIndentInc, JsonTextMode aMode)
        {
            aSB.Append((m_Data) ? "true" : "false");
        }
        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;
            if (obj is bool boolean)
                return m_Data == boolean;
            return false;
        }
        public override int GetHashCode()
        {
            return m_Data.GetHashCode();
        }
        public override void Clear()
        {
            m_Data = false;
        }
    }
    // End of JsonBool

    public partial class JsonNull : JsonNode
    {
        static readonly JsonNull m_StaticInstance = new JsonNull();
        public static bool reuseSameInstance = true;
        public static JsonNull CreateOrGet()
        {
            if (reuseSameInstance)
                return m_StaticInstance;
            return new JsonNull();
        }
        private JsonNull() { }

        public override JsonNodeType Tag { get { return JsonNodeType.NullValue; } }
        public override bool IsNull { get { return true; } }
        public override Enumerator GetEnumerator() { return new Enumerator(); }

        public override string Value
        {
            get { return "null"; }
            set { }
        }
        public override bool AsBool
        {
            get { return false; }
            set { }
        }

        public override JsonNode Clone()
        {
            return CreateOrGet();
        }

        public override bool Equals(object obj)
        {
            if (object.ReferenceEquals(this, obj))
                return true;
            return (obj is JsonNull);
        }
        public override int GetHashCode()
        {
            return 0;
        }

        internal override void WriteToStringBuilder(StringBuilder aSB, int aIndent, int aIndentInc, JsonTextMode aMode)
        {
            aSB.Append("null");
        }
    }
    // End of JsonNull

    internal partial class JsonLazyCreator : JsonNode
    {
        private JsonNode m_Node = null;
        private readonly string m_Key = null;
        public override JsonNodeType Tag { get { return JsonNodeType.None; } }
        public override Enumerator GetEnumerator() { return new Enumerator(); }

        public JsonLazyCreator(JsonNode aNode)
        {
            m_Node = aNode;
            m_Key = null;
        }

        public JsonLazyCreator(JsonNode aNode, string aKey)
        {
            m_Node = aNode;
            m_Key = aKey;
        }

        private T Set<T>(T aVal) where T : JsonNode
        {
            if (m_Key == null)
                m_Node.Add(aVal);
            else
                m_Node.Add(m_Key, aVal);
            m_Node = null; // Be GC friendly.
            return aVal;
        }

        public override JsonNode this[int aIndex]
        {
            get { return new JsonLazyCreator(this); }
            set { Set(new JsonArray()).Add(value); }
        }

        public override JsonNode this[string aKey]
        {
            get { return new JsonLazyCreator(this, aKey); }
            set { Set(new JsonObject()).Add(aKey, value); }
        }

        public override void Add(JsonNode aItem)
        {
            Set(new JsonArray()).Add(aItem);
        }

        public override void Add(string aKey, JsonNode aItem)
        {
            Set(new JsonObject()).Add(aKey, aItem);
        }

        public static bool operator ==(JsonLazyCreator a, object b)
        {
            if (b == null)
                return true;
            return ReferenceEquals(a, b);
        }

        public static bool operator !=(JsonLazyCreator a, object b)
        {
            return !(a == b);
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return true;
            return ReferenceEquals(this, obj);
        }

        public override int GetHashCode()
        {
            return 0;
        }

        public override int AsInt
        {
            get { Set(new JsonNumber(0)); return 0; }
            set { Set(new JsonNumber(value)); }
        }

        public override float AsFloat
        {
            get { Set(new JsonNumber(0.0f)); return 0.0f; }
            set { Set(new JsonNumber(value)); }
        }

        public override double AsDouble
        {
            get { Set(new JsonNumber(0.0)); return 0.0; }
            set { Set(new JsonNumber(value)); }
        }

        public override long AsLong
        {
            get
            {
                if (longAsString)
                    Set(new JsonString("0"));
                else
                    Set(new JsonNumber(0.0));
                return 0L;
            }
            set
            {
                if (longAsString)
                    Set(new JsonString(value.ToString()));
                else
                    Set(new JsonNumber(value));
            }
        }

        public override ulong AsULong
        {
            get
            {
                if (longAsString)
                    Set(new JsonString("0"));
                else
                    Set(new JsonNumber(0.0));
                return 0L;
            }
            set
            {
                if (longAsString)
                    Set(new JsonString(value.ToString()));
                else
                    Set(new JsonNumber(value));
            }
        }

        public override bool AsBool
        {
            get { Set(new JsonBool(false)); return !false; }
            set { Set(new JsonBool(value)); }
        }

        public override JsonArray AsArray
        {
            get { return Set(new JsonArray()); }
        }

        public override JsonObject AsObject
        {
            get { return Set(new JsonObject()); }
        }
        internal override void WriteToStringBuilder(StringBuilder aSB, int aIndent, int aIndentInc, JsonTextMode aMode)
        {
            aSB.Append("null");
        }
    }
	public partial class JsonNode
    {
        #region Decimal
        public virtual decimal AsDecimal
        {
            get
            {
                if (!decimal.TryParse(Value, out decimal result))
                    result = 0;
                return result;
            }
            set
            {
                Value = value.ToString();
            }
        }

        public static implicit operator JsonNode(decimal aDecimal)
        {
            return new JsonString(aDecimal.ToString());
        }

        public static implicit operator decimal(JsonNode aNode)
        {
            return aNode.AsDecimal;
        }
        #endregion Decimal

        #region Char
        public virtual char AsChar
        {
            get
            {
                if (IsString && Value.Length > 0)
                    return Value[0];
                if (IsNumber)
                    return (char)AsInt;
                return '\0';
            }
            set
            {
                if (IsString)
                    Value = value.ToString();
                else if (IsNumber)
                    AsInt = (int)value;
            }
        }

        public static implicit operator JsonNode(char aChar)
        {
            return new JsonString(aChar.ToString());
        }

        public static implicit operator char(JsonNode aNode)
        {
            return aNode.AsChar;
        }
        #endregion Decimal

        #region UInt
        public virtual uint AsUInt
        {
            get
            {
                return (uint)AsDouble;
            }
            set
            {
                AsDouble = value;
            }
        }

        public static implicit operator JsonNode(uint aUInt)
        {
            return new JsonNumber(aUInt);
        }

        public static implicit operator uint(JsonNode aNode)
        {
            return aNode.AsUInt;
        }
        #endregion UInt

        #region Byte
        public virtual byte AsByte
        {
            get
            {
                return (byte)AsInt;
            }
            set
            {
                AsInt = value;
            }
        }

        public static implicit operator JsonNode(byte aByte)
        {
            return new JsonNumber(aByte);
        }

        public static implicit operator byte(JsonNode aNode)
        {
            return aNode.AsByte;
        }
        #endregion Byte
        #region SByte
        public virtual sbyte AsSByte
        {
            get
            {
                return (sbyte)AsInt;
            }
            set
            {
                AsInt = value;
            }
        }

        public static implicit operator JsonNode(sbyte aSByte)
        {
            return new JsonNumber(aSByte);
        }

        public static implicit operator sbyte(JsonNode aNode)
        {
            return aNode.AsSByte;
        }
        #endregion SByte

        #region Short
        public virtual short AsShort
        {
            get
            {
                return (short)AsInt;
            }
            set
            {
                AsInt = value;
            }
        }

        public static implicit operator JsonNode(short aShort)
        {
            return new JsonNumber(aShort);
        }

        public static implicit operator short(JsonNode aNode)
        {
            return aNode.AsShort;
        }
        #endregion Short
        #region UShort
        public virtual ushort AsUShort
        {
            get
            {
                return (ushort)AsInt;
            }
            set
            {
                AsInt = value;
            }
        }

        public static implicit operator JsonNode(ushort aUShort)
        {
            return new JsonNumber(aUShort);
        }

        public static implicit operator ushort(JsonNode aNode)
        {
            return aNode.AsUShort;
        }
        #endregion UShort

        #region DateTime
        public virtual System.DateTime AsDateTime
        {
            get
            {
                if (!System.DateTime.TryParse(Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime result))
                    result = new System.DateTime(0);
                return result;
            }
            set
            {
                Value = value.ToString(CultureInfo.InvariantCulture);
            }
        }

        public static implicit operator JsonNode(System.DateTime aDateTime)
        {
            return new JsonString(aDateTime.ToString(CultureInfo.InvariantCulture));
        }

        public static implicit operator System.DateTime(JsonNode aNode)
        {
            return aNode.AsDateTime;
        }
        #endregion DateTime
        #region TimeSpan
        public virtual System.TimeSpan AsTimeSpan
        {
            get
            {
                if (!System.TimeSpan.TryParse(Value, CultureInfo.InvariantCulture, out System.TimeSpan result))
                    result = new System.TimeSpan(0);
                return result;
            }
            set
            {
                Value = value.ToString();
            }
        }

        public static implicit operator JsonNode(System.TimeSpan aTimeSpan)
        {
            return new JsonString(aTimeSpan.ToString());
        }

        public static implicit operator System.TimeSpan(JsonNode aNode)
        {
            return aNode.AsTimeSpan;
        }
        #endregion TimeSpan

        #region Guid
        public virtual System.Guid AsGuid
        {
            get
            {
                System.Guid.TryParse(Value, out Guid result);
                return result;
            }
            set
            {
                Value = value.ToString();
            }
        }

        public static implicit operator JsonNode(System.Guid aGuid)
        {
            return new JsonString(aGuid.ToString());
        }

        public static implicit operator System.Guid(JsonNode aNode)
        {
            return aNode.AsGuid;
        }
        #endregion Guid

        #region ByteArray
        public virtual byte[] AsByteArray
        {
            get
            {
                if (this.IsNull || !this.IsArray)
                    return null;
                int count = Count;
                byte[] result = new byte[count];
                for (int i = 0; i < count; i++)
                    result[i] = this[i].AsByte;
                return result;
            }
            set
            {
                if (!IsArray || value == null)
                    return;
                Clear();
                for (int i = 0; i < value.Length; i++)
                    Add(value[i]);
            }
        }

        public static implicit operator JsonNode(byte[] aByteArray)
        {
            return new JsonArray { AsByteArray = aByteArray };
        }

        public static implicit operator byte[](JsonNode aNode)
        {
            return aNode.AsByteArray;
        }
        #endregion ByteArray
        #region ByteList
        public virtual List<byte> AsByteList
        {
            get
            {
                if (this.IsNull || !this.IsArray)
                    return null;
                int count = Count;
                List<byte> result = new List<byte>(count);
                for (int i = 0; i < count; i++)
                    result.Add(this[i].AsByte);
                return result;
            }
            set
            {
                if (!IsArray || value == null)
                    return;
                Clear();
                for (int i = 0; i < value.Count; i++)
                    Add(value[i]);
            }
        }

        public static implicit operator JsonNode(List<byte> aByteList)
        {
            return new JsonArray { AsByteList = aByteList };
        }

        public static implicit operator List<byte> (JsonNode aNode)
        {
            return aNode.AsByteList;
        }
        #endregion ByteList

        #region StringArray
        public virtual string[] AsStringArray
        {
            get
            {
                if (this.IsNull || !this.IsArray)
                    return null;
                int count = Count;
                string[] result = new string[count];
                for (int i = 0; i < count; i++)
                    result[i] = this[i].Value;
                return result;
            }
            set
            {
                if (!IsArray || value == null)
                    return;
                Clear();
                for (int i = 0; i < value.Length; i++)
                    Add(value[i]);
            }
        }

        public static implicit operator JsonNode(string[] aStringArray)
        {
            return new JsonArray { AsStringArray = aStringArray };
        }

        public static implicit operator string[] (JsonNode aNode)
        {
            return aNode.AsStringArray;
        }
        #endregion StringArray
        #region StringList
        public virtual List<string> AsStringList
        {
            get
            {
                if (this.IsNull || !this.IsArray)
                    return null;
                int count = Count;
                List<string> result = new List<string>(count);
                for (int i = 0; i < count; i++)
                    result.Add(this[i].Value);
                return result;
            }
            set
            {
                if (!IsArray || value == null)
                    return;
                Clear();
                for (int i = 0; i < value.Count; i++)
                    Add(value[i]);
            }
        }

        public static implicit operator JsonNode(List<string> aStringList)
        {
            return new JsonArray { AsStringList = aStringList };
        }

        public static implicit operator List<string> (JsonNode aNode)
        {
            return aNode.AsStringList;
        }
        #endregion StringList

        #region NullableTypes
        public static implicit operator JsonNode(int? aValue)
        {
            if (aValue == null)
                return JsonNull.CreateOrGet();
            return new JsonNumber((int)aValue);
        }
        public static implicit operator int?(JsonNode aNode)
        {
            if (aNode == null || aNode.IsNull)
                return null;
            return aNode.AsInt;
        }

        public static implicit operator JsonNode(float? aValue)
        {
            if (aValue == null)
                return JsonNull.CreateOrGet();
            return new JsonNumber((float)aValue);
        }
        public static implicit operator float? (JsonNode aNode)
        {
            if (aNode == null || aNode.IsNull)
                return null;
            return aNode.AsFloat;
        }

        public static implicit operator JsonNode(double? aValue)
        {
            if (aValue == null)
                return JsonNull.CreateOrGet();
            return new JsonNumber((double)aValue);
        }
        public static implicit operator double? (JsonNode aNode)
        {
            if (aNode == null || aNode.IsNull)
                return null;
            return aNode.AsDouble;
        }

        public static implicit operator JsonNode(bool? aValue)
        {
            if (aValue == null)
                return JsonNull.CreateOrGet();
            return new JsonBool((bool)aValue);
        }
        public static implicit operator bool? (JsonNode aNode)
        {
            if (aNode == null || aNode.IsNull)
                return null;
            return aNode.AsBool;
        }

        public static implicit operator JsonNode(long? aValue)
        {
            if (aValue == null)
                return JsonNull.CreateOrGet();
            return new JsonNumber((long)aValue);
        }
        public static implicit operator long? (JsonNode aNode)
        {
            if (aNode == null || aNode.IsNull)
                return null;
            return aNode.AsLong;
        }

        public static implicit operator JsonNode(short? aValue)
        {
            if (aValue == null)
                return JsonNull.CreateOrGet();
            return new JsonNumber((short)aValue);
        }
        public static implicit operator short? (JsonNode aNode)
        {
            if (aNode == null || aNode.IsNull)
                return null;
            return aNode.AsShort;
        }
        #endregion NullableTypes
    }
}