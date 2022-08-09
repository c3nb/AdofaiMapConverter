using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AdofaiMapConverter.Types;
using JSON;

namespace AdofaiMapConverter.Actions
{
    public class UnknownAction : Action
    {
        private JsonNode raw;
        public UnknownAction() : base(LevelEventType.None) { }
        public UnknownAction(JsonNode raw) : this()
            => Raw = raw;
        public JsonNode Raw 
        { 
            get => raw; 
            set
            {
                active = value["active"].AsBool;
                raw = value;
            }
        }
        public override JsonNode ToNode()
            => Raw;
    }
}
