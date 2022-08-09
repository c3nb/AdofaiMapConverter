using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AdofaiMapConverter.Types;
using AdofaiMapConverter.Actions;
using AdofaiMapConverter.Helpers;

namespace AdofaiMapConverter.Converters
{
    public static class OuterConverter
    {
        public static CustomLevel Convert(CustomLevel customLevel)
        {
            return MapConverterBase.Convert(customLevel, ae =>
            {
                List<Tile> oneTimingTiles = ae.oneTimingTiles;
                List<Tile> newTiles = oneTimingTiles.Select(t => t.Copy()).ToList();
                if (ae.floor == 1)
                {
                    Tile firstTile = newTiles[0];
                    if (!firstTile.GetActions(LevelEventType.Twirl).Any())
                        firstTile.AddAction(new Twirl());
                    else firstTile.GetActions(LevelEventType.Twirl).Clear();
                }
                return newTiles;
            });
        }
    }
}
