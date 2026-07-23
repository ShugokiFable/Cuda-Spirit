# Local JSON import format

Cuda Spirit recursively scans up to 1,000 `.json` files in the selected directory. Files larger than 512 MB are skipped. Unchanged files are not reparsed until their size or modification timestamp changes.

The importer is intentionally tolerant. It recognizes common aliases from different export tools.

## Knowledge records

An object becomes a knowledge record when it contains at least a name/title or ID/key.

Recognized fields include:

| Purpose | Accepted aliases |
|---|---|
| Name | `name`, `title`, `itemName`, `displayName`, `Name` |
| ID | `id`, `key`, `itemId`, `mainKey`, `nodeId`, `skillId`, `recipeId` |
| Description | `description`, `desc`, `tooltip`, `summary`, `effect`, `contents` |
| Kind | `kind`, `type`, `category` |
| Region | `region` |
| Updated date | `updatedAt`, `effectiveAt`, `patchDate` |

File names containing `recipe`, `skill`, `grind`, `spot`, `node`, `route`, `map`, `item`, or `equipment` help infer the record kind.

## Route and farm nodes

A route node needs a name and at least one routing value, such as coordinates, gear recommendation, or expected silver per hour.

| Purpose | Accepted aliases |
|---|---|
| Key | `key`, `id`, `nodeId`, `zoneId` |
| Name | `name`, `title`, `zoneName`, `nodeName` |
| AP | `recommendedAp`, `ap`, `minAp`, `recAp` |
| DP | `recommendedDp`, `dp`, `minDp`, `recDp` |
| Silver/hour | `expectedSilverPerHour`, `silverPerHour`, `silverHour`, `profitPerHour`, `avgSilverPerHour` |
| X | `x`, `posX`, `worldX`, `longitude` |
| Y | `y`, `posY`, `worldY`, `latitude` |
| Risk | `risk`, `danger`, `riskScore`, from `0.0` to `1.0` |
| Territory | `territory`, `region`, `area` |

## Route edges

| Purpose | Accepted aliases |
|---|---|
| From | `from`, `fromKey`, `source`, `start` |
| To | `to`, `toKey`, `target`, `end`, `destination` |
| Travel minutes | `travelMinutes`, `minutes`, `durationMinutes`, `time` |
| Risk | `risk`, `danger` |
| Bidirectional | `bidirectional`, `twoWay`, `bothWays` |
| Transport | `transport`, `mode`, `type` |

Both endpoint keys must exist as route nodes. Matching is case-insensitive and saved using the canonical node key.

## Important

`data/example-routes.json` is illustrative schema data, not a current Black Desert recommendation dataset. Replace its values with current, sourced data for your region and patch.
