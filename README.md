## Introduction

In the current version of the game, enemy health bars disappear if you don't deal damage to them for at least three seconds. This plugin increases that duration, and allows enemies at low health and/or nearby the player to remain visible indefinitely. Allies now also reveal their target upon dealing damage. Transparency can be changed as well, to reduce visual obstruction.

All of these parameters are easily customizable via the built-in [*r2modman*](https://thunderstore.io/package/ebkr/r2modman/) configuration editor. Simply change the options provided in the file `local.healthbar.viewer.cfg` - please refer to each entry description for more details. Feel free to report any feedback or issues discovered [here](https://github.com/6thmoon/HealthBarViewer/issues).

## Version History

#### `1.0.0`
- Fixed *Seekers of the Storm* compatibility.

#### `0.2.0`
- Add option to control allied damage for multiplayer.

#### `0.1.2`
- Transparency/opacity can now be adjusted.

#### `0.1.1`
- Aiming directly at an enemy will refresh the health bar indicator.

#### `0.1.0`
- Introduce option to limit visibility beyond a certain range.
- All parameters are now configurable.

#### `0.0.2`
- Reduce duration based on health threshold.

#### `0.0.1`
- Enemy health bars remain visible indefinitely.
