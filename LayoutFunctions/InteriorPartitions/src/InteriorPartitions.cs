using Elements;
using Elements.Geometry;
using System.Collections.Generic;
using System.Linq;
using LayoutFunctionCommon;

namespace InteriorPartitions
{
    public static class InteriorPartitions
    {
        /// <summary>
        /// The InteriorPartitions function.
        /// </summary>
        /// <param name="model">The input model.</param>
        /// <param name="input">The arguments to the execution.</param>
        /// <returns>A InteriorPartitionsOutputs instance containing computed results and the model with any new elements.</returns>
        public static InteriorPartitionsOutputs Execute(Dictionary<string, Model> inputModels, InteriorPartitionsInputs input)
        {
            var interiorPartitionCandidates = new List<InteriorPartitionCandidate>();
            var modelDependencies = new[] {
                "Private Office Layout",
                "Phone Booth Layout",
                "Classroom Layout",
                "Meeting Room Layout",
                "Space Planning Zones",
                "Bedroom Layout",
                "Living Room Layout",
                "Kitchen Layout",
                "Workshop Layout",
                "Home Office Layout",
                "Bathroom Layout",
                "Restroom Layout",
                "Laundry Room Layout",
                "Entertainment Room Layout",
                "Room Layout"
                 };
            foreach (var md in modelDependencies)
            {
                if (inputModels.TryGetValue(md, out var mdModel))
                {
                    interiorPartitionCandidates.AddRange(mdModel?.AllElementsOfType<InteriorPartitionCandidate>());
                }

            }

            var output = new InteriorPartitionsOutputs();
            // TODO: don't assume one height for all walls on a level — pass height through deduplication.
            var levelGroups = interiorPartitionCandidates.Where(c => c.WallCandidateLines.Count > 0).GroupBy(c => c.LevelTransform);
            foreach (var levelGroup in levelGroups)
            {
                var canidates = WallGeneration.DeduplicateWallLines(levelGroup.ToList());
                var height = levelGroup.OrderBy(l => l.Height).FirstOrDefault()?.Height ?? 3;
                WallGeneration.GenerateWalls(output.Model, canidates, height, levelGroup.Key);
            }

            return output;
        }
    }
}