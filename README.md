# InstantUntie

Oxide plugin for Rust. Instantly untie underwater boxes.

Allows players to untie the underwater boxes with the duration set in the config.

## Permission

* `instantuntie.use` - allows players to use instant untie

## Configuration

```json
{
  "Untie Duration (Seconds)": 0.0,
  "How often to check if player is underwater (Seconds)": 10.0,
  "How often to check if a player is holding the use button (Seconds)": 1.0,
  "Show Untie Message": true,
  "Show canceled message": true,
  "Buoyancy Scale": 0.0,
  "Set box owner as untie player": false
}
```

## Localization

```json
{
  "Chat": "<color=#bebebe>[<color=#de8732>Instant Untie</color>] {0}</color>",
  "Untie": "The box will untie in {0} seconds. Please hold the use key down until this is completed.",
  "UntieCanceled": "You have canceled untying the box. Please hold the use key down to untie."
}
```
