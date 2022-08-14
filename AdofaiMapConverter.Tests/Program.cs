using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using JSON;
using System.Reflection;
using AdofaiMapConverter.Actions;
using AdofaiMapConverter.Types;
using AdofaiMapConverter.Helpers;
using AdofaiMapConverter.Converters;
using AdofaiMapConverter.Converters.Effects;

namespace AdofaiMapConverter.Tests
{
    internal class Program
    {
        static void Main(string[] args)
        {
            CustomLevel lev = CustomLevel.Read(JsonNode.Parse(File.ReadAllText("Plum-R2 _Return to_\\main.adofai")));
            //CustomLevel il = CustomLevel.Read(JsonNode.Parse(File.ReadAllText("iL_Remake\\iL.adofai")));
            var level = NonEffectConverter.Convert(LinearConverter.Convert(lev), LevelEventType.MoveCamera).ToNode().ToString(4);
            File.WriteAllText("Plum-R2 _Return to_\\COPY.adofai", level, Encoding.UTF8);
        }
        public static IEnumerable<double> Range(int from, int to, int unit = 1)
        {
            for (int i = from; i <= to; i += unit)
                yield return i;
        }
        static void GenActClasses()
        {
            Assembly amcAsm = typeof(Tile).Assembly;
            StringBuilder sb = new StringBuilder();
            foreach (Type type in amcAsm.GetTypes().Where(t => t.Namespace?.Contains("AdofaiMapConverter.Actions") ?? false))
            {
                string typeName = type.Name;
                if (typeName.Contains("Action")) continue;
                object tInstance = Activator.CreateInstance(type);
                sb.AppendLine($"public class {typeName} : Action");
                sb.AppendLine("{");
                var fields = type.GetFields((BindingFlags)15422);
                foreach (FieldInfo field in fields)
                {
                    Type ft = field.FieldType;
                    string fieldName = field.Name;
                    if (fieldName == "active" || fieldName == "eventType")
                        continue;
                    string fieldNameL = $"_{field.Name}";
                    string ftName = FilterTypeName(ft);
                    Indent(4).AppendLine($"public {ftName} {fieldName}");
                    Indent(4).AppendLine("{");
                    Indent(8).AppendLine($"get => {fieldNameL};");
                    Indent(8).AppendLine("set");
                    Indent(8).AppendLine("{");
                    Indent(12).AppendLine($"{fieldName}flag = true;");
                    Indent(12).AppendLine($"{fieldNameL} = value;");
                    Indent(8).AppendLine("}");
                    Indent(4).AppendLine("}");

                    object val = field.GetValue(tInstance);
                    string valStr = string.Empty;
                    if (val == null)
                        Console.WriteLine(field + field.DeclaringType.ToString());
                    Type valType = val.GetType();
                    if (valType.IsEnum)
                        valStr = $"{valType.Name}.{val}";
                    else if (valType == typeof((int, TileRelativeTo)))
                    {
                        (int, TileRelativeTo) tuple = ((int, TileRelativeTo))val;
                        valStr = $"({tuple.Item1}, TileRelativeTo.{tuple.Item2})";
                    }
                    else if (valType == typeof(Vector2))
                    {
                        Vector2 vec = (Vector2)val;
                        if (vec == Vector2.One)
                            valStr = "Vector2.One";
                        else if (vec == Vector2.MOne)
                            valStr = "Vector2.MOne";
                        else if (vec == Vector2.Zero)
                            valStr = "Vector2.Zero";
                        else if (vec == Vector2.Hrd)
                            valStr = "Vector2.Hrd";
                        else valStr = $"new Vector2({vec.x}, {vec.y})";
                    }
                    else if (valType == typeof(string))
                        valStr = $"\"{val}\"";
                    else valStr = val.ToString();
                    Indent(4).AppendLine($"private {ftName} {fieldNameL} = {valStr};");
                    Indent(4).AppendLine($"private bool {fieldName}flag;");
                }

                Indent(4).AppendLine($"public {typeName}() : base(LevelEventType.{typeName}) {{ }}");
                string parametersStr = string.Empty;
                if (fields.Length == 0)
                    parametersStr = "bool active";
                else if (fields.Length == 1)
                    parametersStr = $"{FilterTypeName(fields[0].FieldType)} {fields[0].Name}, bool active";
                else
                {
                    parametersStr = fields.Select(f => (f.FieldType, f.Name))
                    .Aggregate("", (s, tuple) => $"{s}{FilterTypeName(tuple.FieldType)} {tuple.Name}, ");
                    parametersStr = parametersStr.Remove(parametersStr.Length - 2) + ", bool active";
                }
                    
                Indent(4).AppendLine($"public {typeName}({parametersStr}) : base(LevelEventType.{typeName}, active)");
                Indent(4).AppendLine("{");
                foreach (FieldInfo field in fields.Where(f => f.Name != "active" && f.Name != "eventType"))
                    Indent(8).AppendLine($"this.{field.Name} = {field.Name};");
                Indent(4).AppendLine("}");

                Indent(4).AppendLine("public override JsonNode ToNode()");
                Indent(4).AppendLine("{");
                Indent(8).AppendLine("JsonNode node = InitNode(eventType, active);");
                foreach (FieldInfo field in fields)
                {
                    Type ft = field.FieldType;
                    string fieldName = field.Name;
                    if (fieldName == "active" || fieldName == "eventType")
                        continue;
                    string fieldNameL = $"_{field.Name}";
                    string ftName = FilterTypeName(ft);
                    Indent(8).AppendLine($"if ({fieldName}flag)");
                    WriteToNode(field, 12);
                }
                Indent(8).AppendLine("return node;");
                Indent(4).AppendLine("}");
                sb.AppendLine("}");
            }
            Console.WriteLine(sb);
            StringBuilder Indent(int size)
                => sb.Append(' ', size);
            string FilterTypeName(Type type)
            {
                if (type == typeof((int, TileRelativeTo)))
                    return "(int, TileRelativeTo)";
                else if (type == typeof(bool))
                    return "bool";
                else if (type == typeof(string))
                    return "string";
                else if (type == typeof(int))
                    return "int";
                else if (type == typeof(double))
                    return "double";
                else return type.Name;
            }
            void WriteToNode(FieldInfo field, int indentSize)
            {
                string fn = field.Name;
                Type type = field.FieldType;
                if (type.IsEnum)
                    sb.Append(' ', indentSize).AppendLine($"node[\"{fn}\"] = _{fn}.ToString();");
                else if (type == typeof(Vector2))
                    sb.Append(' ', indentSize).AppendLine($"node[\"{fn}\"] = _{fn}.ToNode();");
                else if (type == typeof((int, TileRelativeTo)))
                {
                    int braceIS = Math.Max(0, indentSize - 4);
                    sb.Append(' ', braceIS).AppendLine("{");
                    sb.Append(' ', indentSize).AppendLine($"JsonArray _{fn}Arr = new JsonArray();");
                    sb.Append(' ', indentSize).AppendLine($"_{fn}Arr[0] = {fn}.Item1;");
                    sb.Append(' ', indentSize).AppendLine($"_{fn}Arr[1] = {fn}.Item2.ToString();");
                    sb.Append(' ', indentSize).AppendLine($"node[\"{fn}\"] = _{fn}Arr;");
                    sb.Append(' ', braceIS).AppendLine("}");
                }
                else sb.Append(' ', indentSize).AppendLine($"node[\"{fn}\"] = _{fn};");
            }
        }
        static void GenActs()
        {
            Assembly amcAsm = typeof(Tile).Assembly;
            StringBuilder sb = new StringBuilder();
            int arrUsedCount = 0;
            sb.AppendLine("public static Action ParseAction(JsonNode node)").AppendLine("{");
            sb.Append(' ', 4).AppendLine("switch (node[\"eventType\"].ToString().TrimLR().Parse<LevelEventType>())");
            sb.Append(' ', 4).AppendLine("{");
            foreach (Type type in amcAsm.GetTypes().Where(t => t.Namespace?.Contains("AdofaiMapConverter.Actions") ?? false))
            {
                string typeName = type.Name;
                if (typeName == "Action" || typeName == "ActionUtils")
                    continue;
                string vn = typeName.ToLower();
                //Console.WriteLine(type);
                PropertyInfo[] props = type.GetProperties();
                sb.Append(' ', 8).AppendLine($"case LevelEventType.{typeName}:");
                sb.Append(' ', 12).AppendLine($"{typeName} {vn} = new {typeName}();");
                foreach (PropertyInfo prop in props)
                {
                    Type ft = prop.PropertyType;
                    sb.Append(' ', 12).AppendLine($"if (!CheckIsNull(node[\"{prop.Name}\"]))");
                    sb.Append(' ', 16);
                    if (ft.IsEnum)
                        sb.AppendLine($"{vn}.{prop.Name} = node[\"{prop.Name}\"].ToString().TrimLR().Parse<{ft.Name}>();");
                    else if (ft == typeof(double))
                        sb.AppendLine($"{vn}.{prop.Name} = node[\"{prop.Name}\"].AsDouble;");
                    else if (ft == typeof(int))
                        sb.AppendLine($"{vn}.{prop.Name} = node[\"{prop.Name}\"].AsInt;");
                    else if (ft == typeof(string))
                        sb.AppendLine($"{vn}.{prop.Name} = node[\"{prop.Name}\"].ToString().TrimLR();");
                    else if (ft == typeof(Vector2))
                        sb.AppendLine($"{vn}.{prop.Name} = Vector2.FromNode(node[\"{prop.Name}\"]);");
                    else if (ft == typeof(bool))
                        sb.AppendLine($"{vn}.{prop.Name} = node[\"{prop.Name}\"].AsBool;");
                    else if (ft == typeof((int, TileRelativeTo)))
                    {
                        sb.Append(' ', 12).AppendLine("{");
                        var lowerFieldName = prop.Name.ToLower();
                        if (arrUsedCount != 0)
                        {
                            lowerFieldName += arrUsedCount + 1;
                            sb.AppendLine($"JsonArray {lowerFieldName}Arr = node[\"{prop.Name}\"].AsArray;");
                            sb.Append(' ', 16).Append($"(int, TileRelativeTo) {lowerFieldName} = ");
                            sb.AppendLine($"({lowerFieldName}Arr[0].AsInt, {lowerFieldName}Arr[1].ToString().TrimLR().Parse<TileRelativeTo>());");
                            sb.Append(' ', 16).AppendLine($"{vn}.{prop.Name} = {lowerFieldName};");
                        }
                        else
                        {
                            sb.AppendLine($"JsonArray {lowerFieldName}Arr = node[\"{prop.Name}\"].AsArray;");
                            sb.Append(' ', 16).Append($"(int, TileRelativeTo) {lowerFieldName} = ");
                            sb.AppendLine($"({lowerFieldName}Arr[0].AsInt, {lowerFieldName}Arr[1].ToString().TrimLR().Parse<TileRelativeTo>());");
                            sb.Append(' ', 16).AppendLine($"{vn}.{prop.Name} = {lowerFieldName};");
                        }
                        sb.Append(' ', 12).AppendLine("}");
                        arrUsedCount++;
                    }
                    else Console.WriteLine($"SKIPPED?!?! (decType:{prop.DeclaringType}, pType:{ft})");
                }
                sb.Append(' ', 12).AppendLine($"if (!CheckIsNull(node[\"active\"]))");
                sb.Append(' ', 16).AppendLine($"{vn}.active = node[\"active\"].AsBool;");
                sb.Append(' ', 12).AppendLine($"return {vn};");
            }
            sb.Append(' ', 4).AppendLine("}");
            sb.AppendLine("}");
            Console.WriteLine(sb);
        }
        static void GenDecClasses()
        {
            Assembly amcAsm = typeof(Tile).Assembly;
            StringBuilder sb = new StringBuilder();
            foreach (Type type in amcAsm.GetTypes().Where(t => t.Namespace?.Contains("AdofaiMapConverter.Decorations") ?? false))
            {
                string typeName = type.Name;
                if (typeName == "Decoration" || typeName == "DecorationUtils" || typeName == "UnknownDecoration") continue;
                object tInstance = Activator.CreateInstance(type);
                sb.AppendLine($"public class {typeName} : Decoration");
                sb.AppendLine("{");
                var fields = type.GetFields((BindingFlags)15422);
                foreach (FieldInfo field in fields)
                {
                    Type ft = field.FieldType;
                    string fieldName = field.Name;
                    if (fieldName == "active" || fieldName == "eventType")
                        continue;
                    string fieldNameL = $"_{field.Name}";
                    string ftName = FilterTypeName(ft);
                    Indent(4).AppendLine($"public {ftName} {fieldName}");
                    Indent(4).AppendLine("{");
                    Indent(8).AppendLine($"get => {fieldNameL};");
                    Indent(8).AppendLine("set");
                    Indent(8).AppendLine("{");
                    Indent(12).AppendLine($"{fieldName}flag = true;");
                    Indent(12).AppendLine($"{fieldNameL} = value;");
                    Indent(8).AppendLine("}");
                    Indent(4).AppendLine("}");

                    object val = field.GetValue(tInstance);
                    string valStr = string.Empty;
                    if (val == null)
                        Console.WriteLine(field + field.DeclaringType.ToString());
                    Type valType = val.GetType();
                    if (valType.IsEnum)
                        valStr = $"{valType.Name}.{val}";
                    else if (valType == typeof((int, TileRelativeTo)))
                    {
                        (int, TileRelativeTo) tuple = ((int, TileRelativeTo))val;
                        valStr = $"({tuple.Item1}, TileRelativeTo.{tuple.Item2})";
                    }
                    else if (valType == typeof(Vector2))
                    {
                        Vector2 vec = (Vector2)val;
                        if (vec == Vector2.One)
                            valStr = "Vector2.One";
                        else if (vec == Vector2.MOne)
                            valStr = "Vector2.MOne";
                        else if (vec == Vector2.Zero)
                            valStr = "Vector2.Zero";
                        else if (vec == Vector2.Hrd)
                            valStr = "Vector2.Hrd";
                        else valStr = $"new Vector2({vec.x}, {vec.y})";
                    }
                    else if (valType == typeof(string))
                        valStr = $"\"{val}\"";
                    else valStr = val.ToString();
                    Indent(4).AppendLine($"private {ftName} {fieldNameL} = {valStr};");
                    Indent(4).AppendLine($"private bool {fieldName}flag;");
                }

                Indent(4).AppendLine($"public {typeName}() : base(LevelEventType.{typeName}) {{ }}");
                string parametersStr = string.Empty;
                if (fields.Length == 0)
                    parametersStr = "bool active";
                else if (fields.Length == 1)
                    parametersStr = $"{FilterTypeName(fields[0].FieldType)} {fields[0].Name}, bool active";
                else
                {
                    parametersStr = fields.Select(f => (f.FieldType, f.Name))
                    .Aggregate("", (s, tuple) => $"{s}{FilterTypeName(tuple.FieldType)} {tuple.Name}, ");
                    parametersStr = parametersStr.Remove(parametersStr.Length - 2) + ", bool active";
                }

                Indent(4).AppendLine($"public {typeName}({parametersStr}) : base(LevelEventType.{typeName}, active)");
                Indent(4).AppendLine("{");
                foreach (FieldInfo field in fields.Where(f => f.Name != "active" && f.Name != "eventType"))
                    Indent(8).AppendLine($"this.{field.Name} = {field.Name};");
                Indent(4).AppendLine("}");

                Indent(4).AppendLine("public override JsonNode ToNode()");
                Indent(4).AppendLine("{");
                Indent(8).AppendLine("JsonNode node = InitNode(eventType, floor, visible);");
                foreach (FieldInfo field in fields)
                {
                    Type ft = field.FieldType;
                    string fieldName = field.Name;
                    if (fieldName == "active" || fieldName == "eventType")
                        continue;
                    string fieldNameL = $"_{field.Name}";
                    string ftName = FilterTypeName(ft);
                    Indent(8).AppendLine($"if ({fieldName}flag)");
                    WriteToNode(field, 12);
                }
                Indent(8).AppendLine("return node;");
                Indent(4).AppendLine("}");
                sb.AppendLine("}");
            }
            Console.WriteLine(sb);
            StringBuilder Indent(int size)
                => sb.Append(' ', size);
            string FilterTypeName(Type type)
            {
                if (type == typeof((int, TileRelativeTo)))
                    return "(int, TileRelativeTo)";
                else if (type == typeof(bool))
                    return "bool";
                else if (type == typeof(string))
                    return "string";
                else if (type == typeof(int))
                    return "int";
                else if (type == typeof(double))
                    return "double";
                else return type.Name;
            }
            void WriteToNode(FieldInfo field, int indentSize)
            {
                string fn = field.Name;
                Type type = field.FieldType;
                if (type.IsEnum)
                    sb.Append(' ', indentSize).AppendLine($"node[\"{fn}\"] = _{fn}.ToString();");
                else if (type == typeof(Vector2))
                    sb.Append(' ', indentSize).AppendLine($"node[\"{fn}\"] = _{fn}.ToNode();");
                else if (type == typeof((int, TileRelativeTo)))
                {
                    int braceIS = Math.Max(0, indentSize - 4);
                    sb.Append(' ', braceIS).AppendLine("{");
                    sb.Append(' ', indentSize).AppendLine($"JsonArray _{fn}Arr = new JsonArray();");
                    sb.Append(' ', indentSize).AppendLine($"_{fn}Arr[0] = {fn}.Item1;");
                    sb.Append(' ', indentSize).AppendLine($"_{fn}Arr[1] = {fn}.Item2.ToString();");
                    sb.Append(' ', indentSize).AppendLine($"node[\"{fn}\"] = _{fn}Arr;");
                    sb.Append(' ', braceIS).AppendLine("}");
                }
                else sb.Append(' ', indentSize).AppendLine($"node[\"{fn}\"] = _{fn};");
            }
        }
        static void GenDecs()
        {
            Assembly amcAsm = typeof(Tile).Assembly;
            StringBuilder sb = new StringBuilder();
            int arrUsedCount = 0;
            sb.AppendLine("public static Decoration ParseDecoration(JsonNode node)").AppendLine("{");
            sb.Append(' ', 4).AppendLine("switch (node[\"eventType\"].ToString().TrimLR().Parse<LevelEventType>())");
            sb.Append(' ', 4).AppendLine("{");
            foreach (Type type in amcAsm.GetTypes().Where(t => t.Namespace?.Contains("AdofaiMapConverter.Decorations") ?? false))
            {
                string typeName = type.Name;
                if (typeName == "Decoration" || typeName == "DecorationUtils" || typeName == "UnknownDecoration")
                    continue;
                string vn = typeName.ToLower();
                //Console.WriteLine(type);
                PropertyInfo[] props = type.GetProperties();
                sb.Append(' ', 8).AppendLine($"case LevelEventType.{typeName}:");
                sb.Append(' ', 12).AppendLine($"{typeName} {vn} = new {typeName}();");
                foreach (PropertyInfo prop in props)
                {
                    Type ft = prop.PropertyType;
                    sb.Append(' ', 12).AppendLine($"if (!CheckIsNull(node[\"{prop.Name}\"]))");
                    sb.Append(' ', 16);
                    if (ft.IsEnum)
                        sb.AppendLine($"{vn}.{prop.Name} = node[\"{prop.Name}\"].ToString().TrimLR().Parse<{ft.Name}>();");
                    else if (ft == typeof(double))
                        sb.AppendLine($"{vn}.{prop.Name} = node[\"{prop.Name}\"].AsDouble;");
                    else if (ft == typeof(int))
                        sb.AppendLine($"{vn}.{prop.Name} = node[\"{prop.Name}\"].AsInt;");
                    else if (ft == typeof(string))
                        sb.AppendLine($"{vn}.{prop.Name} = node[\"{prop.Name}\"].ToString().TrimLR();");
                    else if (ft == typeof(Vector2))
                        sb.AppendLine($"{vn}.{prop.Name} = Vector2.FromNode(node[\"{prop.Name}\"]);");
                    else if (ft == typeof(bool))
                        sb.AppendLine($"{vn}.{prop.Name} = node[\"{prop.Name}\"].AsBool;");
                    else if (ft == typeof((int, TileRelativeTo)))
                    {
                        sb.Append(' ', 12).AppendLine("{");
                        var lowerFieldName = prop.Name.ToLower();
                        if (arrUsedCount != 0)
                        {
                            lowerFieldName += arrUsedCount + 1;
                            sb.AppendLine($"JsonArray {lowerFieldName}Arr = node[\"{prop.Name}\"].AsArray;");
                            sb.Append(' ', 16).Append($"(int, TileRelativeTo) {lowerFieldName} = ");
                            sb.AppendLine($"({lowerFieldName}Arr[0].AsInt, {lowerFieldName}Arr[1].ToString().TrimLR().Parse<TileRelativeTo>());");
                            sb.Append(' ', 16).AppendLine($"{vn}.{prop.Name} = {lowerFieldName};");
                        }
                        else
                        {
                            sb.AppendLine($"JsonArray {lowerFieldName}Arr = node[\"{prop.Name}\"].AsArray;");
                            sb.Append(' ', 16).Append($"(int, TileRelativeTo) {lowerFieldName} = ");
                            sb.AppendLine($"({lowerFieldName}Arr[0].AsInt, {lowerFieldName}Arr[1].ToString().TrimLR().Parse<TileRelativeTo>());");
                            sb.Append(' ', 16).AppendLine($"{vn}.{prop.Name} = {lowerFieldName};");
                        }
                        sb.Append(' ', 12).AppendLine("}");
                        arrUsedCount++;
                    }
                    else Console.WriteLine($"SKIPPED?!?! (decType:{prop.DeclaringType}, pType:{ft})");
                }
                sb.Append(' ', 12).AppendLine($"if (!CheckIsNull(node[\"visible\"]))");
                sb.Append(' ', 16).AppendLine($"{vn}.visible = node[\"visible\"].AsBool;");
                sb.Append(' ', 12).AppendLine($"return {vn};");
            }
            sb.Append(' ', 4).AppendLine("}");
            sb.AppendLine("}");
            Console.WriteLine(sb);
        }
        static void GenLS()
        {
            FieldInfo[] fields = typeof(LevelSetting).GetFields();
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                var ft = field.FieldType;
                sb.Append("newSetting.");
                if (ft.IsEnum)
                    sb.AppendLine($"{field.Name} = node[\"{field.Name}\"].ToString().TrimLR().Parse<{ft.Name}>();");
                else if (ft == typeof(double))
                    sb.AppendLine($"{field.Name} = node[\"{field.Name}\"].AsDouble;");
                else if (ft == typeof(int))
                    sb.AppendLine($"{field.Name} = node[\"{field.Name}\"].AsInt;");
                else if (ft == typeof(string))
                    sb.AppendLine($"{field.Name} = node[\"{field.Name}\"].ToString().TrimLR();");
                else if (ft == typeof(Vector2))
                    sb.AppendLine($"{field.Name} = Vector2.FromNode(node[\"{field.Name}\"]);");
                else if (ft == typeof(bool))
                    sb.AppendLine($"{field.Name} = node[\"{field.Name}\"].AsBool;");
                else Console.WriteLine($"Skipped!?!? ({field})");
            }
            Console.WriteLine(sb);
        }
        static void GenLSCopy()
        {
            FieldInfo[] fields = typeof(LevelSetting).GetFields();
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                sb.Append("newSetting.").AppendLine($"{field.Name} = {field.Name};");
            }
            Console.WriteLine(sb);
        }
    }
}
