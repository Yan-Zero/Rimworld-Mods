using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace DeepRim
{
    public class Command_TargetLayer : Command_Action
    {
        public UndergroundManager manager;
        public Building thing;

        public override void ProcessInput(Event ev)
        {
            Find.WindowStack.Add(MakeMenu());
        }

        private FloatMenu MakeMenu()
        {
            List<FloatMenuOption> list = new List<FloatMenuOption>();

            //是竖井
            if (thing is Building_MiningShaft)
                if (((Building_MiningShaft)thing).curMode == 0)
                {
                    list.Add(new FloatMenuOption("NewLayer".Translate(), delegate
                    {
                        ((Building_MiningShaft)thing).drillNew = true;
                    }));
                    foreach (KeyValuePair<int, UndergroundMapParent> pair in manager.layersState)
                    {
                        list.Add(new FloatMenuOption("Depth".Translate() + pair.Key + "0m", delegate
                        {
                            ((Building_MiningShaft)thing).drillNew = false;
                            ((Building_MiningShaft)thing).targetedLevel = pair.Key;
                        }));
                    }
                }
                else
                    list.Add(new FloatMenuOption("CantChangeTarget".Translate(), null));

            //是货运电梯
            else if (thing is Building_FreightElevator)
                if (((Building_FreightElevator)thing).curMode == 0)
                {
                    if (((Building_FreightElevator)thing).Depth != 0)
                        list.Add(new FloatMenuOption("Yan.Target.Surface".Translate(), delegate
                        {
                            ((Building_FreightElevator)thing).targetedLevel = 0;
                        }));

                    foreach (KeyValuePair<int, UndergroundMapParent> pair in manager.layersState)
                    {
                        if (((Building_FreightElevator)thing).Depth != pair.Key)
                            list.Add(new FloatMenuOption("Depth".Translate() + pair.Key + "0m", delegate
                            {
                                ((Building_FreightElevator)thing).targetedLevel = pair.Key;
                            }));
                    }
                }
                else
                    list.Add(new FloatMenuOption("CantChangeTarget".Translate(), null));

            if(list.Count == 0)
                list.Add(new FloatMenuOption("Yan.Target.Hasno".Translate(), null));

            return new FloatMenu(list);
        }
    }

}