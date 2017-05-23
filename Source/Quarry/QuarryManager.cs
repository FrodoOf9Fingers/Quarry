﻿using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;
using Verse;

namespace Quarry {

  public class QuarryManager : MapComponent {

    private delegate IntVec3 Del_Offset(IntVec3 newLoc);
    private static   IntVec3 offsetUL(IntVec3 basePos) { return basePos + new IntVec3(-3, 0,  3); }
    private static   IntVec3 offsetUR(IntVec3 basePos) { return basePos + new IntVec3( 3, 0,  3); }
    private static   IntVec3 offsetLL(IntVec3 basePos) { return basePos + new IntVec3(-3, 0, -3); }
    private static   IntVec3 offsetLR(IntVec3 basePos) { return basePos + new IntVec3( 3, 0, -3); }

    public QuarryManager(Map map) : base(map) {
    }

    public List<QuarryResource> resources;
    public bool Spawned {
      get {
        return (FindQuarryBase() != null);
      }
    }

    private List<ThingDef> rockTypes;
    public List<ThingDef> RockTypes {
      get {
        // If the list is empty for some reason, generate a new one
        if (rockTypes == null) {
          foreach (IntVec3 c in GenAdj.CellsOccupiedBy(Base)) {
            string rockType = c.GetTerrain(map).label.Split(' ').Last().CapitalizeFirst();
            if (DefDatabase<ThingDef>.GetNamed("Chunk" + rockType, false) != null) {
              rockTypes.Add(DefDatabase<ThingDef>.GetNamed("Chunk" + rockType));
            }
          }
        }
        return rockTypes;
      }
      set {
        rockTypes = value;
      }
    }

    private Quarry_Base baseInt;
    public Quarry_Base Base {
      get {
        if (baseInt == null) {
          baseInt = FindQuarryBase();
        }
        return baseInt;
      }
    }

    private List<Quarry_Quadrant> quadsInt;
    public List<Quarry_Quadrant> Quads {
      get {
        if (quadsInt == null) {
          quadsInt = FindQuarryQuads();
        }
        return quadsInt;
      }
    }


    public override void ExposeData() {
      base.ExposeData();
      Scribe_References.Look(ref baseInt, "QRY_QuarryManager_Base");
      Scribe_Collections.Look(ref rockTypes, "QRY_QuarryManager_RockTypes");
    }


    private Quarry_Base FindQuarryBase () {
      List<Thing> allThings = map.listerThings.AllThings;
      for (int i = 0; i < allThings.Count; i++) {
        if (allThings[i] is Quarry_Base) {
          return allThings[i] as Quarry_Base;
        }
      }
      return null;
    }


    private List<Quarry_Quadrant> FindQuarryQuads() {

      // In order to find quads, a base is needed
      if (baseInt == null) {
        StringBuilder stringBuilder = new StringBuilder();
        stringBuilder.Append("No quarry was found while trying to get a list of quadrants. Trying to find one. - ");
        baseInt = FindQuarryBase();
        if (baseInt == null) {
          stringBuilder.Append("Unable to find a quarry.");
          Log.Error(stringBuilder.ToString());
          return null;
        }
        stringBuilder.Append("Quarry found.");
        Log.Warning(stringBuilder.ToString());
      }

      List<Quarry_Quadrant> foundQuads = new List<Quarry_Quadrant>();

      // Setup offsets
      Del_Offset Del_UL = new Del_Offset(offsetUL);
      Del_Offset Del_UR = new Del_Offset(offsetUR);
      Del_Offset Del_LL = new Del_Offset(offsetLL);
      Del_Offset Del_LR = new Del_Offset(offsetLR);

      // Manually add the quadrants
      foundQuads.Add(Del_UL(baseInt.Position).GetEdifice(map) as Quarry_Quadrant);
      foundQuads.Add(Del_UR(baseInt.Position).GetEdifice(map) as Quarry_Quadrant);
      foundQuads.Add(Del_LL(baseInt.Position).GetEdifice(map) as Quarry_Quadrant);
      foundQuads.Add(Del_LR(baseInt.Position).GetEdifice(map) as Quarry_Quadrant);

      if (foundQuads == null) {
        return null;
      }
      return foundQuads;
    }


    private List<Quarry_Quadrant> FindAllQuads() {
      List<Building> buildings = Find.VisibleMap.listerBuildings.allBuildingsColonist;
      List<Quarry_Quadrant> allQuads = new List<Quarry_Quadrant>();
      for (int i = 0; i < buildings.Count; i++) {
        if (buildings[i] is Quarry_Quadrant) {
          allQuads.Add(buildings[i] as Quarry_Quadrant);
        }
        if (allQuads.Count == 4) {
          break;
        }
      }
      return allQuads;
    }


    // This gets called from Building_QuarryBase on every SpawnSetup()
    // (every time a new one is built or the game is loaded)
    // or by Item_Conglomerate if there's no list present
    public void FindResources() {
      // Create a new list of QuarryResources and start
      // populating the list
      resources = new List<QuarryResource>();
      BuildResourceList();
    }


    public void BuildResourceList() {
      foreach (SimpleQuarryResource resource in DefDatabase<QuarryResourceDef>.GetNamed("Resources")) {
        if (DefDatabase<ThingDef>.GetNamed(resource.thingDef, false) != null) {

          ThingDef resourceDef = ThingDef.Named(resource.thingDef);

          resources.Add(new QuarryResource(
            resourceDef,
            resource.probability + OverResource(resourceDef, resource.probability),
            resource.stackCount,
            resource.largeVein));
        }
      }
    }


    // If there is a deep resource located under the quarry, increase the odds it will be mined
    private int OverResource(ThingDef checkingDef, int currProbability) {
      foreach (IntVec3 c in GenAdj.CellsOccupiedBy(Base)) {
        if (checkingDef == Find.VisibleMap.deepResourceGrid.ThingDefAt(c)) {
          return Rand.RangeInclusive(1, Mathf.Max(2, Mathf.FloorToInt(currProbability / 4)));
        }
      }
      return 0;
    }


    public void Register(Quarry_Base quarryBase) {
      baseInt = quarryBase;
      quadsInt = FindQuarryQuads();
    }


    // Destroy all the quadrants then deregister
    public void DeconstructQuarry() {
      // Rebuild the internal list, to prevent an error when a
      // quadrant is deleted in dev mode
      quadsInt = FindAllQuads();
      foreach (Quarry_Quadrant quad in quadsInt) {
        quad.Destroy(); 
      }
      // Deregister all pieces of the quarry
      Deregister();
    }


    public void Deregister() {
      baseInt = null;
      quadsInt.Clear();
    }
  }
}
