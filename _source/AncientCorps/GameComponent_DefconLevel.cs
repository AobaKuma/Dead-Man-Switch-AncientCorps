using RimWorld;
using System;
using System.Linq;
using Verse;

namespace AncientCorps
{
    public class GameComponent_DefconLevel : GameComponent
    {
        public int Level => level;

        //0級開始，最高五級
        private int level;
        private int historicalLevel = 0;
        private int actionInterval = 120000;

        //每個等級的下降時間，如果攻擊了設施就會重置時間。
        private int[] intervalPerLevel = new int[]
        {
            240000,
            240000, //4天
            720000, //12天
            900000, //15天
            1800000, //30天
            3600000  //60天
        };
        private int interval;
        public void LevelUP()
        {
            level++;
            if (level > 5) level = 5;
            if (level > historicalLevel) historicalLevel = level;
            ResetInterval();
        }
        public void LevelDown()
        {
            if (level > 0) level--;
            if (level > 0) NotifyLevelDown();
            ResetInterval();
        }
        private void NotifyLevelDown()
        {
            var taggedString = "DMSAC_DefconLevel.0".Translate();
            switch (level)
            {
                case 1:
                    // I級警戒: 機兵師在其地面設施部屬了常態化的巡邏部隊，使受襲設施的增援間隔縮短。
                    taggedString = "DMSAC_DefconLevel.1".Translate();
                    break;
                case 2:
                    // II級警戒: 根據當前局勢，機兵師獲得對
                    taggedString = "DMSAC_DefconLevel.2".Translate();
                    break;
                case 3:
                    // III級警戒: 機兵師在其地面設施部屬了常態化的巡邏部隊，使受襲設施的增援間隔縮短。
                    taggedString = "DMSAC_DefconLevel.3".Translate();
                    break;
                case 4:
                    // IV級警戒: 根據殖民艦隊公約的戒備狀態條例，並根據海牙第三公約進行了宣戰，並已達到調派戰鬥部隊的合規條件。
                    // 部隊將在遵守國際人道法和《羅馬規約》條例的前提下對已識別的敵方基地進行打擊。
                    taggedString = "DMSAC_DefconLevel.4".Translate();
                    break;
                case 5:
                    // V級警戒: 根據殖民艦隊公約的戒備狀態條例，機兵師確認海牙第一公約已不再適用於當前狀況，並已達到合法派出軍隊的合規條件。
                    // 部隊將在遵守國際人道法和《羅馬規約》條例的前提下對已識別的敵方基地進行打擊。
                    taggedString = "DMSAC_DefconLevel.5".Translate();
                    break;
            }
            //根據近期觀察的活動變化，(機兵師)指揮部下降了其內部的警戒等級，現在它們的警戒等級為:
            Find.LetterStack.ReceiveLetter("DMSAC_DefconLevel_Down".Translate(), "DMSAC_DefconLevel_Down_Desc".Translate(DMS_DefOf.DMS_AncientCorps.LabelCap, level) + taggedString, LetterDefOf.NegativeEvent);
        }

        public void ResetInterval()
        {
            if (level < intervalPerLevel.Length) interval = intervalPerLevel[level];
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            switch (level)
            {
                default:
                    break;
                case 0:
                    break;
                case 1:
                    break;
                case 2:
                    break;
                case 3:
                    break;
                case 4:
                    break;
                case 5:
                    break;
            }
        }
    }
}