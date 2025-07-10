using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace AncientCorps
{
    public class GameCondition_HuntingCorps : GameCondition
    {
        //古機只要還存在就會主動的襲擊你，每次襲擊後有5%機率提升等級，並且強度會隨著警戒等級提升，會伴隨更多的打擊類型。
        public override string TooltipString
        {
            get
            {
                Vector2 location = ((Find.CurrentMap == null) ? default(Vector2) : Find.WorldGrid.LongLatOf(Find.CurrentMap.Tile));
                string text = def.LabelCap;
                text += "\n";
                text = text + "\n" + Description;
                text = string.Concat(text, "\n", "ImpactDate".Translate().CapitalizeFirst(), ": ", GenDate.DateFullStringAt(GenDate.TickGameToAbs(startTick + base.Duration), location).Colorize(ColoredText.DateTimeColor));
                return string.Concat(text, "\n", "TimeLeft".Translate().CapitalizeFirst(), ": ", base.TicksLeft.ToStringTicksToPeriod().Colorize(ColoredText.DateTimeColor));
            }
        }

        public override void GameConditionTick()
        {
            base.GameConditionTick();

        }

        public override void End()
        {
        }

        private void Impact()
        {
            ScreenFader.SetColor(Color.clear);
            GenGameEnd.EndGameDialogMessage("GameOverPlanetkillerImpact".Translate(Find.World.info.name), allowKeepPlaying: false, FadeColor);
        }
    }
}