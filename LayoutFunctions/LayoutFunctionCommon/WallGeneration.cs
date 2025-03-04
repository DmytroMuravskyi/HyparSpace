﻿using System;
using System.Collections.Generic;
using System.Linq;
using Elements;
using Elements.Geometry;
using Elements.Spatial;

namespace LayoutFunctionCommon
{
    public static class WallGeneration
    {
        private static double mullionSize = 0.07;
        private static double doorWidth = 0.9;
        private static double doorHeight = 2.1;
        private static double sideLightWidth = 0.4;

        private static Dictionary<string, int> interiorPartitionTypePriority = new Dictionary<string, int>()
        {
            {"Solid", 3},
            {"Partition", 2},
            {"Glass", 1}
        };

        private static Material wallMat = new Material("Drywall", new Color(0.9, 0.9, 0.9, 1.0), 0.01, 0.01);
        private static Material glassMat = new Material("Glass", new Color(0.7, 0.7, 0.7, 0.3), 0.3, 0.6);
        private static Material mullionMat = new Material("Storefront Mullions", new Color(0.5, 0.5, 0.5, 1.0));
        public static List<(Line line, string type)> FindWallCandidates(ISpaceBoundary room, Profile levelProfile, IEnumerable<Line> corridorSegments, out Line orientationGuideEdge, IEnumerable<string> wallTypeFilter = null)
        {
            var spaceBoundary = room.Boundary;
            var wallCandidateLines = new List<(Line line, string type)>();
            orientationGuideEdge = FindPrimaryAccessEdge(spaceBoundary.Perimeter.Segments().Select(s => s.TransformedLine(room.Transform)), corridorSegments, levelProfile, out var wallCandidates);
            wallCandidateLines.Add((orientationGuideEdge, "Glass"));
            if (levelProfile != null)
            {
                var exteriorWalls = FindAllEdgesAdjacentToSegments(wallCandidates, levelProfile.Segments(), out var notAdjacentToFloorBoundary);
                wallCandidateLines.AddRange(notAdjacentToFloorBoundary.Select(s => (s, "Solid")));
            }
            else
            {
                // if no level or floor is present, everything that's not glass is solid.
                wallCandidateLines.AddRange(wallCandidates.Select(s => (s, "Solid")));
            }
            if (wallTypeFilter != null)
            {
                return wallCandidateLines.Where((w) => wallTypeFilter.Contains(w.type)).ToList();
            }
            return wallCandidateLines;
        }

        public static List<(Line OrientationGuideEdge, List<(Line Line, string Type)> WallCandidates)> FindWallCandidateOptions(ISpaceBoundary room, Profile levelProfile, IEnumerable<Line> corridorSegments, IEnumerable<string> wallTypeFilter = null)
        {
            var wallCandidateOptions = new List<(Line OrientationGuideEdge, List<(Line Line, string Type)> WallCandidates)>();
            var allSegments = room.Boundary.Perimeter.Segments().Select(s => s.TransformedLine(room.Transform)).ToList();
            var orientationGuideEdges = SortEdgesByPrimaryAccess(allSegments, corridorSegments, levelProfile);
            foreach (var orientationGuideEdge in orientationGuideEdges)
            {
                var wallCandidateLines = new List<(Line line, string type)>
                {
                    (orientationGuideEdge.Line, "Glass")
                };
                if (levelProfile != null)
                {
                    var exteriorWalls = FindAllEdgesAdjacentToSegments(orientationGuideEdge.OtherSegments, levelProfile.Segments(), out var notAdjacentToFloorBoundary);
                    wallCandidateLines.AddRange(notAdjacentToFloorBoundary.Select(s => (s, "Solid")));
                }
                else
                {
                    // if no level or floor is present, everything that's not glass is solid.
                    wallCandidateLines.AddRange(orientationGuideEdge.OtherSegments.Select(s => (s, "Solid")));
                }
                wallCandidateOptions.Add((orientationGuideEdge.Line, (wallTypeFilter != null ? wallCandidateLines.Where((w) => wallTypeFilter.Contains(w.type)).ToList() : wallCandidateLines)));
            }

            return wallCandidateOptions;
        }

        public static List<(Line line, string type)> DeduplicateWallLines(List<InteriorPartitionCandidate> interiorPartitionCandidates)
        {
            var resultCandidates = new List<(Line line, string type)>();
            var typedLines = interiorPartitionCandidates.SelectMany(c => c.WallCandidateLines)
                            .Where(l => interiorPartitionTypePriority.Keys.Contains(l.type));
            var collinearLinesGroups = GroupCollinearLines(typedLines);

            foreach (var collinearLinesGroup in collinearLinesGroups)
            {
                if (collinearLinesGroup.Value.Count == 1)
                {
                    resultCandidates.Add(collinearLinesGroup.Value.First());
                    continue;
                }
                var linesOrderedByLength = collinearLinesGroup.Value.OrderByDescending(v => v.line.Length());
                var dominantLineForGroup = linesOrderedByLength.First().line;
                var domLineDir = dominantLineForGroup.Direction();

                var orderEnds = new List<(double pos, bool isEnd, string type)>();
                foreach (var linePair in collinearLinesGroup.Value)
                {
                    var line = linePair.line;
                    var start = (line.Start - dominantLineForGroup.Start).Dot(domLineDir);
                    var end = (line.End - dominantLineForGroup.Start).Dot(domLineDir);
                    if (start > end)
                    {
                        var oldStart = start;
                        start = end;
                        end = oldStart;
                    }

                    orderEnds.Add((start, false, linePair.type));
                    orderEnds.Add((end, true, linePair.type));
                }

                var totalCount = 0;
                (double pos, bool isEnd, string type) segmentStart = default;
                var endsOrdered = orderEnds.OrderBy(e => e.pos).ThenBy(e => e.isEnd);
                var typePointsCounts = new Dictionary<string, int>();
                foreach (var point in endsOrdered)
                {
                    var prevCount = totalCount;
                    typePointsCounts.TryGetValue(point.type, out var typeCount);
                    var delta = point.isEnd ? -1 : 1;
                    totalCount += delta;
                    typeCount += delta;
                    typePointsCounts[point.type] = typeCount;
                    if (totalCount == 1 && prevCount == 0) // begin segment
                    {
                        segmentStart = point;
                    }
                    else if (totalCount == 0) // end segment
                    {
                        AddWallCandidateLine(resultCandidates, dominantLineForGroup, domLineDir, segmentStart, point);
                    }
                    else if (segmentStart.type.Equals(point.type))
                    {
                        if (typePointsCounts[segmentStart.type] == 0) // end segment with current type
                        {
                            AddWallCandidateLine(resultCandidates, dominantLineForGroup, domLineDir, segmentStart, point);
                            var nextType = typePointsCounts
                                .FirstOrDefault(t => interiorPartitionTypePriority[t.Key] < interiorPartitionTypePriority[point.type]
                                            && t.Value > 0);
                            if (nextType.Key != null) // start segment with lower priority if it exists
                            {
                                segmentStart = (point.pos, false, nextType.Key);
                            }
                        }
                    }
                    else
                    {
                        // new type with higher priority starts
                        if (interiorPartitionTypePriority[point.type] > interiorPartitionTypePriority[segmentStart.type])
                        {
                            AddWallCandidateLine(resultCandidates, dominantLineForGroup, domLineDir, segmentStart, (point.pos, point.isEnd, segmentStart.type));
                            segmentStart = point;
                        }
                    }
                }
            }

            return resultCandidates;
        }

        private static Dictionary<Line, List<(Line line, string type)>> GroupCollinearLines(IEnumerable<(Line line, string type)> typedLines)
        {
            var collinearLinesGroups = new Dictionary<Line, List<(Line line, string type)>>();
            foreach (var typedLine in typedLines)
            {
                var isLineAdded = false;
                foreach (var linesGroup in collinearLinesGroups)
                {
                    if (typedLine.line.IsCollinear(linesGroup.Key))
                    {
                        linesGroup.Value.Add(typedLine);
                        isLineAdded = true;
                        break;
                    }
                }
                if (!isLineAdded)
                {
                    collinearLinesGroups.Add(typedLine.line, new List<(Line line, string type)>() { typedLine });
                }
            }

            return collinearLinesGroups;
        }

        private static void AddWallCandidateLine(List<(Line line, string type)> resultCandidates, Line dominantLineForGroup, Vector3 domLineDir, (double pos, bool isEnd, string type) segmentStart, (double pos, bool isEnd, string type) point)
        {
            var startPt = segmentStart.pos * domLineDir + dominantLineForGroup.Start;
            var endPt = point.pos * domLineDir + dominantLineForGroup.Start;
            if (startPt.DistanceTo(endPt) > 0.01)
            {
                var newLine = new Line(startPt, endPt);
                resultCandidates.Add((newLine, point.type));
            }
        }

        private static double CalculateTotalStorefrontHeight(double volumeHeight)
        {
            return Math.Min(2.7, volumeHeight);
        }

        private static GeometricElement CreateMullion(double height)
        {
            var totalStorefrontHeight = CalculateTotalStorefrontHeight(height);
            var mullion = new Mullion
            {
                BaseLine = new Line(new Vector3(-mullionSize / 2, 0, 0), new Vector3(mullionSize / 2, 0, 0)),
                Width = mullionSize,
                Height = totalStorefrontHeight,
                Material = mullionMat,
                IsElementDefinition = true
            };
            return mullion;
        }

        public static void GenerateWalls(Model model, List<(Line line, string type)> wallCandidateLines, double height, Transform levelTransform, bool debugMode = false)
        {
            if (debugMode)
            {
                foreach (var wallCandidate in wallCandidateLines)
                {
                    Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(wallCandidate.line));
                    var lineProjected = wallCandidate.line.TransformedLine(new Transform(0, 0, -wallCandidate.line.End.Z));
                    switch (wallCandidate.type)
                    {
                        case "Solid":
                            model.AddElement(new StandardWall(lineProjected, 0.2, height, BuiltInMaterials.ZAxis, levelTransform));
                            break;
                        case "Glass":
                            model.AddElement(new StandardWall(lineProjected, 0.2, height, BuiltInMaterials.XAxis, levelTransform));
                            break;
                        case "Partition":
                            model.AddElement(new StandardWall(lineProjected, 0.2, height, BuiltInMaterials.YAxis, levelTransform));
                            break;
                    }
                }

                return;
            }
            var totalStorefrontHeight = CalculateTotalStorefrontHeight(height);
            var mullion = CreateMullion(height);
            foreach (var (line, type) in wallCandidateLines)
            {
                var lineProjected = line.TransformedLine(new Transform(0, 0, -line.End.Z));

                if (type == "Solid")
                {
                    model.AddElement(new StandardWall(lineProjected, 0.2, height, wallMat, levelTransform));
                }
                else if (type == "Partition")
                {
                    model.AddElement(new StandardWall(lineProjected, 0.1, height, wallMat, levelTransform));
                }
                else if (type == "Glass")
                {
                    var primaryWall = new StorefrontWall(lineProjected, 0.05, height, glassMat, levelTransform);
                    model.AddElement(primaryWall);
                    var grid = new Grid1d(lineProjected);
                    var offsets = new[] { sideLightWidth, sideLightWidth + doorWidth }.Where(o => grid.Domain.Min + o < grid.Domain.Max);
                    grid.SplitAtOffsets(offsets);
                    if (grid.Cells != null && grid.Cells.Count >= 3)
                    {
                        grid[2].DivideByApproximateLength(2);
                    }
                    var separators = grid.GetCellSeparators(true);
                    var beam = new Beam(lineProjected, Polygon.Rectangle(mullionSize, mullionSize), null, mullionMat)
                    {
                        IsElementDefinition = true
                    };
                    var mullionInstances = new[] {
                        beam.CreateInstance(levelTransform, "Base Mullion"),
                        beam.CreateInstance(levelTransform.Concatenated(new Transform(0, 0, doorHeight)), "Base Mullion"),
                        beam.CreateInstance(levelTransform.Concatenated(new Transform(0, 0, totalStorefrontHeight)), "Base Mullion")
                    };
                    foreach (var mullionInstance in mullionInstances)
                    {
                        mullionInstance.AdditionalProperties["Wall"] = primaryWall.Id;
                        model.AddElement(mullionInstance);
                    }
                    foreach (var separator in separators)
                    {
                        // var line = new Line(separator, separator + new Vector3(0, 0, height));
                        // model.AddElement(new ModelCurve(line, BuiltInMaterials.XAxis, levelTransform));
                        var instance = mullion.CreateInstance(new Transform(separator, lineProjected.Direction(), Vector3.ZAxis, 0).Concatenated(levelTransform), "Mullion");
                        instance.AdditionalProperties["Wall"] = primaryWall.Id;
                        model.AddElement(instance);
                    }

                    var headerHeight = height - totalStorefrontHeight;
                    if (headerHeight > 0.01)
                    {
                        var header = new Header(lineProjected, 0.2, headerHeight, wallMat, levelTransform.Concatenated(new Transform(0, 0, totalStorefrontHeight)));
                        header.AdditionalProperties["Wall"] = primaryWall.Id;
                        model.AddElement(header);
                    }
                }
            }
        }

        public static Line FindPrimaryAccessEdge(IEnumerable<Line> edgesToClassify, IEnumerable<Line> corridorSegments, Profile floorBoundary, out IEnumerable<Line> otherSegments, double maxDist = 0)
        {
            if (corridorSegments != null && corridorSegments.Count() != 0)
            {
                // if we have corridors, find the best edge along a corridor
                return FindEdgeAdjacentToSegments(edgesToClassify, corridorSegments, out otherSegments, maxDist);
            }
            else if (floorBoundary != null)
            {
                // if we have no corridors, find the best edge not along the floor boundary
                var edgesAlongFloorBoundary = FindAllEdgesAdjacentToSegments(edgesToClassify, floorBoundary.Segments(), out var edgesNotAlongFloorBoundary);
                var edgesByLength = edgesNotAlongFloorBoundary.OrderByDescending(l => l.Length());
                var bestEdge = edgesByLength.FirstOrDefault();
                if (bestEdge == null)
                {
                    bestEdge = edgesAlongFloorBoundary.OrderBy(e => e.Length()).Last();
                    otherSegments = edgesAlongFloorBoundary.Except(new[] { bestEdge });
                    return bestEdge;
                }
                otherSegments = edgesByLength.Skip(1).Union(edgesAlongFloorBoundary);
                if (otherSegments.Count() != edgesToClassify.Count() - 1)
                {
                    Console.WriteLine("We lost somebody!");
                }
                return bestEdge;
            }
            else
            {
                var edgesByLength = edgesToClassify.OrderByDescending(l => l.Length());
                var bestEdge = edgesByLength.First();
                otherSegments = edgesByLength.Skip(1);
                return bestEdge;
            }
        }

        public static List<Line> FindAllEdgesAdjacentToSegments(IEnumerable<Line> edgesToClassify, IEnumerable<Line> comparisonSegments, out List<Line> otherSegments)
        {
            otherSegments = new List<Line>();
            var adjacentSegments = new List<Line>();

            foreach (var edge in edgesToClassify)
            {
                var midpt = edge.Mid();
                midpt.Z = 0;
                var adjacentToAny = false;
                foreach (var comparisonSegment in comparisonSegments)
                {
                    var start = comparisonSegment.Start;
                    var end = comparisonSegment.End;
                    start.Z = 0;
                    end.Z = 0;
                    var comparisonSegmentProjected = new Line(start, end);
                    var dist = midpt.DistanceTo(comparisonSegmentProjected);
                    if (dist < 0.3)
                    {
                        adjacentToAny = true;
                        adjacentSegments.Add(edge);
                        break;
                    }
                }
                if (!adjacentToAny)
                {
                    otherSegments.Add(edge);
                }
            }
            return adjacentSegments;
        }
        public static Line FindEdgeAdjacentToSegments(IEnumerable<Line> edgesToClassify, IEnumerable<Line> segmentsToTestAgainst, out IEnumerable<Line> otherSegments, double maxDist = 0)
        {
            var minDist = double.MaxValue;
            var minSeg = edgesToClassify.OrderBy(e => e.Length()).Last(); // if no max dist, and no corridor, we just return the longest edge as an orientation guide.
            var allEdges = edgesToClassify.ToList();
            var selectedIndex = 0;
            for (int i = 0; i < allEdges.Count; i++)
            {
                var edge = allEdges[i];
                var midpt = edge.Mid();
                foreach (var seg in segmentsToTestAgainst)
                {
                    var dist = midpt.DistanceTo(seg);
                    // if two segments are basically the same distance to the corridor segment,
                    // prefer the longer one.
                    if (Math.Abs(dist - minDist) < 0.1)
                    {
                        minDist = dist;
                        if (minSeg.Length() < edge.Length())
                        {
                            minSeg = edge;
                            selectedIndex = i;
                        }
                    }
                    else if (dist < minDist)
                    {
                        minDist = dist;
                        minSeg = edge;
                        selectedIndex = i;
                    }
                }
            }
            if (maxDist != 0 && minDist >= maxDist)
            {
                Console.WriteLine($"no matches: {minDist}");
                otherSegments = allEdges;
                return null;
            }
            otherSegments = Enumerable.Range(0, allEdges.Count).Except(new[] { selectedIndex }).Select(i => allEdges[i]);
            return minSeg;
        }

        public static List<(Line Line, IEnumerable<Line> OtherSegments)> SortEdgesByPrimaryAccess(IEnumerable<Line> edgesToClassify, IEnumerable<Line> corridorSegments, Profile floorBoundary, double maxDist = 0)
        {
            if (corridorSegments != null && corridorSegments.Count() != 0)
            {
                // if we have corridors, find the best edge along a corridor
                return FindEdgesAdjacentToSegments(edgesToClassify, corridorSegments, maxDist);
            }

            if (floorBoundary != null)
            {
                // if we have no corridors, find the best edge not along the floor boundary
                var edgesAlongFloorBoundary = FindAllEdgesAdjacentToSegments(edgesToClassify, floorBoundary.Segments(), out var edgesNotAlongFloorBoundary);
                return (edgesNotAlongFloorBoundary.Count() == 0 ? edgesAlongFloorBoundary : edgesNotAlongFloorBoundary).OrderByDescending(e => e.Length()).Select(e =>
                {
                    var otherSegments = edgesToClassify.Except(new[] { e });
                    return (e, otherSegments);
                }).ToList();
            }

            var edgesByLength = edgesToClassify.OrderByDescending(e => e.Length());
            return edgesByLength.Select(e =>
            {
                var otherSegments = edgesToClassify.Except(new[] { e });
                return (e, otherSegments);
            }).ToList();
        }
        public static List<(Line Line, IEnumerable<Line> OtherSegments)> FindEdgesAdjacentToSegments(IEnumerable<Line> edgesToClassify, IEnumerable<Line> segmentsToTestAgainst, double maxDist = 0)
        {
            if (segmentsToTestAgainst.Count() > 0)
            {
                var edgesByDist = edgesToClassify.Select(e =>
                {
                    var midpt = e.Mid();
                    (Line line, double dist) edge = (e, segmentsToTestAgainst.Min(s => midpt.DistanceTo(s)));
                    return edge;
                });

                if (maxDist != 0)
                {
                    edgesByDist = edgesByDist.Where(e => e.dist < maxDist);
                }

                var comparer = new EdgesComparer();
                edgesByDist = edgesByDist.OrderBy(e => e, comparer);

                return edgesByDist.Select(e =>
                {
                    var otherSegments = edgesToClassify.Except(new[] { e.line });
                    return (e.line, otherSegments);
                }).ToList();
            }

            if (maxDist != 0)
            {
                Console.WriteLine($"no matches: {maxDist}");
                return new List<(Line Line, IEnumerable<Line> OtherSegments)>() { (null, edgesToClassify) };
            }

            // if no max dist, and no corridor, we just return the longest edge as an orientation guide.
            var edgesByLength = edgesToClassify.OrderByDescending(e => e.Length());
            return edgesByLength.Select(e =>
            {
                var otherSegments = edgesToClassify.Except(new[] { e });
                return (e, otherSegments);
            }).ToList();
        }

        public static List<(Line line, string type)> PartitionsAndGlazingCandidatesFromGrid(List<(Line line, string type)> wallCandidateLines, Grid2d grid, Profile levelBoundary)
        {
            var wallCandidatesOut = new List<(Line line, string type)>();
            try
            {
                var cellSeparators = grid.GetCellSeparators(GridDirection.V, true);
                wallCandidatesOut.AddRange(cellSeparators.OfType<Line>().Select(c => (c, "Partition")));
            }
            catch (Exception e)
            {
                Console.WriteLine("Couldn't get grid cell separators");
                Console.WriteLine(e.Message);
                // exception in cell separators
            }
            var glassLines = wallCandidateLines.Where(l => l.type == "Glass-Edge").Select(w => w.line);
            foreach (var gridCell in grid.GetCells())
            {
                var trimmedGeo = gridCell.GetTrimmedCellGeometry();
                if (trimmedGeo.Count() > 0)
                {
                    var segments = trimmedGeo.OfType<Polygon>().SelectMany(g => g.Segments());
                    var glassSegment = FindEdgeAdjacentToSegments(segments, glassLines, out var otherEdges);
                    if (glassSegment != null)
                    {
                        wallCandidatesOut.Add((glassSegment, "Glass"));
                    }
                }
            }

            return wallCandidatesOut;
        }
    }

    public class EdgesComparer : IComparer<(Line line, double dist)>
    {
        public int Compare((Line line, double dist) edge1, (Line line, double dist) edge2)
        {
            if (Math.Abs(edge1.dist - edge2.dist) < 0.1)
            {
                return edge2.line.Length().CompareTo(edge1.line.Length());
            }
            return edge1.dist.CompareTo(edge2.dist);
        }
    }
}
