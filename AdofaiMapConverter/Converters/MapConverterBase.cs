using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AdofaiMapConverter.Actions;
using AdofaiMapConverter.Types;
using AdofaiMapConverter.Helpers;

namespace AdofaiMapConverter.Converters
{
    public static class MapConverterBase
    {
        public class ApplyEach
        {
            public ApplyEach()
                => oneTimingTiles = new List<Tile>();
            public ApplyEach(int floor, List<Tile> oneTimingTiles)
            {
                this.floor = floor;
                this.oneTimingTiles = oneTimingTiles;
            }
            public int floor;
            public List<Tile> oneTimingTiles;
        }
        public static CustomLevel ConvertBasedOnTravelAngle(CustomLevel customLevel, Func<Tile, double> travelAngleMapper, Action<Tile> tileProcessor)
            => ConvertBasedOnTravelAngle(customLevel, travelAngleMapper, tileProcessor, tile => { });
        public static CustomLevel ConvertBasedOnTravelAngle(CustomLevel customLevel, Func<Tile, double> travelAngleMapper, Action<Tile> tileProcessor, Action<CustomLevel> customLevelProcessor)
        {
            TileMeta zeroFloorMeta = customLevel.Tiles[0].tileMeta;
            double curStaticAngle = AngleHelper.GetNextStaticAngle(0, zeroFloorMeta.travelAngle, zeroFloorMeta.PlanetAngle, false);
            bool reversed = false;
            int planetNumber = 2;
            return Convert(customLevel, applyEach =>
            {
                return applyEach.oneTimingTiles.Select(tile =>
                {
                    double travelAngle = travelAngleMapper(tile);
                    Tile newTile = tile.Copy();
                    tileProcessor(newTile);
                    if (!newTile.angle.isMidspin)
                        newTile.angle = (TileAngle)curStaticAngle.GeneralizeAngle();
                    if (newTile.actions.GetValueSafeOrAdd(LevelEventType.Twirl, new List<Actions.Action>()).Count % 2 == 1)
                        reversed = !reversed;
                    List<Actions.Action> multiPlanetActions = newTile.actions.GetValueSafeOrAdd(LevelEventType.MultiPlanet, new List<Actions.Action>());
                    if (multiPlanetActions.Any())
                    {
                        MultiPlanet mp = (MultiPlanet)multiPlanetActions[0];
                        planetNumber = mp.planets;
                    }
                    curStaticAngle = AngleHelper.GetNextStaticAngle(curStaticAngle, travelAngle, TileMeta.CalculatePlanetAngle(planetNumber), reversed);
                    return newTile;
                }).ToList();
            }, customLevelProcessor);
        }
        public static CustomLevel Convert(CustomLevel customLevel, Func<ApplyEach, List<Tile>> applyEach)
            => Convert(customLevel, applyEach, c => { });
        public static CustomLevel Convert(CustomLevel customLevel, Func<ApplyEach, List<Tile>> applyEach, Action<CustomLevel> customLevelProcessor)
        {
            List<Tile> tiles = customLevel.Tiles;
            List<Tile> newTiles = new List<Tile>();
            newTiles.Add(new Tile(TileAngle.Zero) { actions = tiles[0].actions });
            List<List<int>> newTileAmounts = new List<List<int>>();
            newTileAmounts.Add(new List<int>() { 0, 1 });
            for (int i = 1; i < tiles.Count;)
            {
                List<Tile> oneTimingTiles = GetSameTimingTiles(tiles, i);
                List<Tile> newTilesResult = applyEach(new ApplyEach(i, oneTimingTiles));
                newTileAmounts.Add(new List<int>() { i, newTilesResult.Count });
                newTiles.AddRange(newTilesResult);
                i += oneTimingTiles.Count;
            }
            CustomLevel newCustomLevel = customLevel.Copy();
            newCustomLevel.Tiles = newTiles;
            customLevelProcessor(newCustomLevel);
            return ArrangeCustomLevelSync(customLevel, newCustomLevel.MakeTiles(), newTileAmounts);
        }
        public static CustomLevel ArrangeCustomLevelSync(CustomLevel oldCustomLevel, CustomLevel newCustomLevel, List<List<int>> newTileAmountPairs)
        {
            CustomLevel finalLevel = newCustomLevel.Copy();
            List<Tile> oldTiles = oldCustomLevel.Tiles;
            List<Tile> newTiles = newCustomLevel.Tiles;
            List<Tile> finalTiles = finalLevel.Tiles;

            List<int> minOldTileNewTileMap = GetOldTileNewTileMap(newTileAmountPairs, oldTiles, MinimumBoundFunc);
            List<int> maxOldTileNewTileMap = GetOldTileNewTileMap(newTileAmountPairs, oldTiles, MaximumBoundFunc);

            double prevBpm = newCustomLevel.Setting.bpm;
            int newTileIdx = 0;
            foreach (List<int> newTileAmountPair in newTileAmountPairs)
            {
                int oldTileIdx = newTileAmountPair[0];
                int newTileAmount = newTileAmountPair[1];

                List<Tile> timingTiles = GetSameTimingTiles(oldTiles, oldTileIdx);
                List<Tile> newTimingTiles = finalTiles.GetRange(newTileIdx, newTileAmount);

                newTimingTiles.ForEach(t => t.actions.Remove(LevelEventType.SetSpeed));

                if (oldTileIdx == 0)
                {
                    double originalZeroTileTravelMs = TileHelper.CalculateZeroTileTravelMs(oldCustomLevel);
                    double newZeroTileTravelMs = TileHelper.CalculateZeroTileTravelMs(newCustomLevel);

                    int originalStraightTravelMs = oldCustomLevel.Setting.offset;
                    double additionalTravelMs = originalZeroTileTravelMs - newZeroTileTravelMs;

                    newCustomLevel.Setting.offset = originalStraightTravelMs + (int)additionalTravelMs;
                }
                else if (newTileIdx + newTileAmount < newTiles.Count)
                {
                    double timingBpm = timingTiles[timingTiles.Count - 1].tileMeta.bpm;
                    double timingTravelAngle = TileMeta.CalculateTotalTravelAndPlanetAngle(timingTiles);
                    double newTravelAngle = TileMeta.CalculateTotalTravelAndPlanetAngle(newTiles.GetRange(newTileIdx, newTileAmount));
                    double multiplyValue = newTravelAngle / timingTravelAngle;
                    double curBpm = timingBpm * multiplyValue;
                    if (!curBpm.FuzzyEquals(prevBpm))
                    {
                        if (!curBpm.IsFinite() || curBpm.FuzzyEquals(0))
                            Console.WriteLine($"Wrong TempBpm Value ({curBpm}, {timingBpm}, multiplyValue={multiplyValue}, newTravelAngle={newTravelAngle}, timingTravelAngle={timingTravelAngle})");
                        newTimingTiles[0].AddAction(new SetSpeed() { speedType = SpeedType.Bpm, beatsPerMinute = curBpm });
                    }
                    newTimingTiles.ForEach(t => FixAction(t, multiplyValue));
                    prevBpm = curBpm;
                }
                for (int i = 0; i < newTimingTiles.Count; i++)
                {
                    int oldTileNum = oldTileIdx + i;
                    int newTileNum = newTileIdx + i;
                    Tile tile = newTimingTiles[i];
                    tile.EditActions(LevelEventType.RecolorTrack,
                        (RecolorTrack rt) =>
                        {
                            rt.startTile = (GetTileNum(minOldTileNewTileMap, oldTiles.Count, finalTiles.Count, oldTileNum, newTileNum, rt.startTile.Item1, rt.startTile.Item2), rt.startTile.Item2);
                            rt.endTile = (GetTileNum(maxOldTileNewTileMap, oldTiles.Count, finalTiles.Count, oldTileNum, newTileNum, rt.endTile.Item1, rt.endTile.Item2), rt.endTile.Item2);
                            return rt;
                        });
                    tile.EditActions(LevelEventType.MoveTrack,
                        (MoveTrack mt) =>
                        {
                            mt.startTile = (GetTileNum(minOldTileNewTileMap, oldTiles.Count, finalTiles.Count, oldTileNum, newTileNum, mt.startTile.Item1, mt.startTile.Item2), mt.startTile.Item2);
                            mt.endTile = (GetTileNum(maxOldTileNewTileMap, oldTiles.Count, finalTiles.Count, oldTileNum, newTileNum, mt.endTile.Item1, mt.endTile.Item2), mt.endTile.Item2);
                            return mt;
                        });
                }
                newTileIdx += newTileAmount;
            }
            return finalLevel.MakeTiles();
        }
        public static List<int> GetOldTileNewTileMap(List<List<int>> newTileAmountPairs, List<Tile> oldTiles, Func<int, int, int, int> boundFunction)
        {
            List<int> oldTileNewTileMap = new List<int>();
            int newTileIdx = 0;
            foreach (List<int> newTileAmountPair in newTileAmountPairs)
            {
                int oldTileIdx = newTileAmountPair[0];
                int newTileAmount = newTileAmountPair[1];
                List<Tile> timingTiles = GetSameTimingTiles(oldTiles, oldTileIdx);
                int oldTileAmount = timingTiles.Count;
                for (int i = 0; i < timingTiles.Count; i++)
                    oldTileNewTileMap.Add(newTileIdx + boundFunction(oldTileAmount, newTileAmount, i));
                newTileIdx += newTileAmount;
            }
            return oldTileNewTileMap;
        }
        public static readonly Func<int, int, int, int> MinimumBoundFunc = (oldTileAmount, newTileAmount, index) => index * newTileAmount / oldTileAmount;
        public static readonly Func<int, int, int, int> MaximumBoundFunc = (oldTileAmount, newTileAmount, index) => (int)Math.Ceiling(((double)(index + 1) * newTileAmount / oldTileAmount) - 1);
        public static int GetTileNum(List<int> oldTileNewTileMap, int maxOldTileNum, int maxNewTileNum, int oldTileNum, int newTileNum, int relativeTileNum, TileRelativeTo tilePosition)
        {
            switch (tilePosition)
            {
                case TileRelativeTo.ThisTile:
                    return oldTileNewTileMap[LimitTileNum(oldTileNum + relativeTileNum, maxOldTileNum)] - newTileNum;
                case TileRelativeTo.Start:
                    return oldTileNewTileMap[LimitTileNum(relativeTileNum, maxOldTileNum)];
                case TileRelativeTo.End:
                    return oldTileNewTileMap[LimitTileNum(maxOldTileNum + relativeTileNum, maxOldTileNum)] + maxNewTileNum - maxOldTileNum;
                default:
                    throw new Exception();
            }
        }
        public static int LimitTileNum(int rawTileNum, int maxTileNum)
            => Math.Min(Math.Max(rawTileNum, 0), maxTileNum - 1);
        public static List<Tile> GetSameTimingTiles(List<Tile> tiles, int fromIndex)
            => TileHelper.GetSameTimingTiles(tiles, fromIndex);
        public static void FixAction(Tile tile, double multiplyValue)
        {
            tile.EditActions(LevelEventType.CustomBackground,
                (CustomBackground cbg) =>
                {
                    cbg.angleOffset *= multiplyValue;
                    return cbg;
                });
            tile.EditActions(LevelEventType.AnimateTrack,
                (AnimateTrack at) =>
                {
                    at.beatsAhead *= multiplyValue;
                    at.beatsBehind *= multiplyValue;
                    return at;
                });
            tile.EditActions(LevelEventType.Flash,
               (Flash f) =>
               {
                   f.angleOffset *= multiplyValue;
                   f.duration *= multiplyValue;
                   return f;
               });
            tile.EditActions(LevelEventType.MoveCamera,
               (MoveCamera mc) =>
               {
                   mc.angleOffset *= multiplyValue;
                   mc.duration *= multiplyValue;
                   return mc;
               });
            tile.EditActions(LevelEventType.RecolorTrack,
               (RecolorTrack rt) =>
               {
                   rt.angleOffset *= multiplyValue;
                   return rt;
               });
            tile.EditActions(LevelEventType.MoveTrack,
               (MoveTrack mt) =>
               {
                   mt.duration *= multiplyValue;
                   mt.angleOffset *= multiplyValue;
                   return mt;
               });
            tile.EditActions(LevelEventType.SetFilter,
               (SetFilter sf) =>
               {
                   sf.angleOffset *= multiplyValue;
                   return sf;
               });
            tile.EditActions(LevelEventType.HallOfMirrors,
               (HallOfMirrors hom) =>
               {
                   hom.angleOffset *= multiplyValue;
                   return hom;
               });
            tile.EditActions(LevelEventType.ShakeScreen,
               (ShakeScreen ss) =>
               {
                   ss.duration *= multiplyValue;
                   ss.angleOffset *= multiplyValue;
                   return ss;
               });
            tile.EditActions(LevelEventType.MoveDecorations,
               (MoveDecorations md) =>
               {
                   md.duration *= multiplyValue;
                   md.angleOffset *= multiplyValue;
                   return md;
               });
            tile.EditActions(LevelEventType.RepeatEvents,
              (RepeatEvents re) =>
              {
                  re.interval *= multiplyValue;
                  return re;
              });
            tile.EditActions(LevelEventType.Bloom,
              (Bloom b) =>
              {
                  b.angleOffset *= multiplyValue;
                  return b;
              });
            tile.EditActions(LevelEventType.PlaySound,
              (PlaySound ps) =>
              {
                  ps.angleOffset *= multiplyValue;
                  return ps;
              });
        }
        public static CustomLevel FixFilterTiming(CustomLevel customLevel)
        {
            CustomLevel newCustomLevel = customLevel.Copy();
            foreach (TileMeta tm in customLevel.Tiles.Select(t => t.tileMeta))
            {
                Tile tile = customLevel.Tiles[tm.floor];
                tile.EditActions(LevelEventType.SetFilter,
                    (SetFilter sf) =>
                    {
                        double angleOffset = sf.angleOffset;
                        if (sf.disableOthers == Toggle.Enabled &&
                            (angleOffset > tm.travelAngle ||
                            angleOffset.FuzzyEquals(tm.travelAngle)))
                            angleOffset = Math.Max(angleOffset - 0.0001, 0);
                        sf.angleOffset = angleOffset;
                        return sf;
                    });
            }
            return newCustomLevel.MakeTiles();
        }
    }
}
