## Introduction

In the current version of the game, enemy health bars disappear if you don't deal damage to them for more than three seconds. However, this plugin increases that duration, and allows enemies at low health and/or nearby the player to remain visible indefinitely. Allies will now also reveal their target upon dealing damage.

All of these parameters are easily customizable via the built-in [*r2modman*](https://thunderstore.io/package/ebkr/r2modman/) configuration editor. Simply change the options provided in the file `local.healthbar.viewer.cfg` - please refer to each entry description for more details. Feel free to report any feedback or issues discovered [here](https://github.com/6thmoon/HealthBarViewer/issues).

## Version History

#### `0.1.1`
- Aiming directly at an enemy will refresh the health bar indicator.

#### `0.1.0`
- Add configuration file; remove placeholder icon.
- Introduce parameter to limit visibility beyond a certain range.
- Publish source code and update documentation.

#### `0.0.2`
- Allies reveal enemy health upon dealing damage.
- Reduced duration to eight seconds for targets above 75% HP.

#### `0.0.1`
- Enemy health bars remain visible indefinitely.
