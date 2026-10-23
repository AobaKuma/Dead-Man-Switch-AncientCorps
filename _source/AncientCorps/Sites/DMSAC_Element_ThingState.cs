using System.Collections.Generic;
using Fortified.Structures;
using RimWorld;
using Verse;

namespace AncientCorps
{
    /// <summary>
    /// 佈局元素：替 pos/size 矩形（或 posList 散點）內已生成的東西上漆、把物品設成禁止拾取。
    /// FFF 的元素式佈局沒有建築顏色欄位，舊版格線佈局的 SymbolDef.color（軍團綠／橄欖）和
    /// 「物品一律禁止」都是靠 Task_SetThingState 做的，這裡把同一個任務接到元素格式上，轉換後外觀不變。
    /// 只發生成任務、不加東西進 sketch，所以放在對應的 Thing 元素之前或之後都可以。
    ///
    /// Layout element: paints whatever got spawned in the pos/size rect (or posList) and forbids items there.
    /// FFF's element layouts have no building colour field; the legacy grid layouts got their SymbolDef.color
    /// (corps green / olive) and their always-forbidden items from Task_SetThingState, so this hooks the same task
    /// up to the element format and converted layouts look the same. Task-only (adds nothing to the sketch).
    /// </summary>
    public class DMSAC_Element_ThingState : FFF_Element, IFFF_TaskProvider
    {
        public IntVec3 pos;
        public IntVec2 size = new IntVec2(1, 1);
        public List<IntVec3> posList;
        public ColorDef color;
        public bool forbidden = true;

        public override void AddToSketch(Sketch sketch) { }

        public List<IFFF_GenerationTask> GetTasks(Rot4 rot, IntVec3 offset)
        {
            List<IFFF_GenerationTask> tasks = new List<IFFF_GenerationTask>();
            if (!posList.NullOrEmpty())
            {
                foreach (IntVec3 p in posList) tasks.Add(Make(p, rot, offset));
            }
            else
            {
                foreach (IntVec3 p in new CellRect(pos.x, pos.z, size.x, size.z)) tasks.Add(Make(p, rot, offset));
            }
            return tasks;
        }

        private IFFF_GenerationTask Make(IntVec3 cell, Rot4 rot, IntVec3 offset)
        {
            return new Task_SetThingState { pos = cell.RotatedBy(rot) + offset, forbidden = forbidden, color = color };
        }
    }
}
