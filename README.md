# Enzo's Bakery

A 2D pixel-art bakery rush game made in Unity 6. Customers walk in, order bread, cookies and cupcakes,
and wait with a patience bar. You grab ingredients, mix the dough, bake it at the right moment, and serve
it before they storm out. Make enough money by closing time to pass each of the three days.

- Design changes from the one-pager: [CHANGES.txt](CHANGES.txt)
- Where every sprite and sound came from: [CREDITS.md](CREDITS.md)

## How to play

| Key | Action |
|---|---|
| **WASD** / arrow keys | walk |
| **Space** | pick up, put down, serve |
| **E** | take a customer's order, pet Biscuit the cat |
| **hold E** | mix dough in a bowl, frost a cupcake |
| **Esc** | pause |
| **M** | music on/off |

Recipes (always flour plus at most one topping):

| | | |
|---|---|---|
| Bread | flour | $10 |
| Cookies | flour + chocolate | $6 |
| Cupcake | flour + berries, bake, then frost | $12 |

1. A customer comes in with a **!** over their head. Stand in front of them and press **E** to take the order.
2. Grab flour (and a topping) from the crates and drop them in a mixing bowl. **Hold E** to mix.
3. Put the dough in an oven. When it dings there's a short golden **PERFECT** window: grab it then for a bigger tip. Wait too long and it burns.
4. Face the customer and press **Space** to serve. Faster service means bigger tips, and 3+ happy customers in a row builds a streak bonus.
5. When customers get grumpy, pet **Biscuit** the cat: everyone waiting gets some patience back (then she naps for a while).

Between days, spend your savings in the upgrade shop.

## Opening the project

1. Install a **Unity 6** editor with Unity Hub (the project was made with 6000.6.2f1).
2. Unity Hub > Add > *Add project from disk* > pick this folder.
3. Open `Assets/Scenes/Bakery.unity` and press Play. The Game view looks best at 1920x1080 (16:9).

The scene is built by an editor script (`Assets/Editor/BakeryBuilder.cs`), so the whole layout lives in one readable file.
If anything in the scene gets messed up, use the menu bar: **Enzo's Bakery > Rebuild Scene**.
Gameplay numbers (day goals, customer frequency, prices, bake times, upgrade prices) can be tuned in the Inspector
on `_Managers` (GameManager and ItemBook components).

**Enzo's Bakery > Build Mac App** makes a standalone build in `Builds/Mac/`.

## Code tour

```
Assets/Scripts
  Core/GameManager.cs        game states, the workday clock, money, streaks, upgrades, the 3-day campaign
  Core/GameData.cs           DaySettings, DayStats, UpgradeDef (data edited in the Inspector)
  Characters/Baker.cs        player movement, picking what's in front of you, carrying items
  Characters/Customer.cs     a customer's life: walk in, wait, order, get served or storm out, pay + tip
  Characters/CustomerSpawner.cs   when customers arrive (lunch rush!), who they are, what they order
  Characters/CustomerLook.cs paper-doll customers assembled from random sprite layers
  Characters/BakeryCat.cs    Biscuit: wanders, can be petted, naps
  Kitchen/Interactable.cs    base class for anything you can face and press Space / E on
  Kitchen/Station.cs         base class for furniture that holds one item
  Kitchen/Counter.cs, IngredientCrate.cs, MixingBowl.cs, Oven.cs, FrostingStation.cs, TrashBin.cs, ServiceSpot.cs
  Kitchen/Item.cs, ItemBook.cs   the things you carry, and their data (prices, bake times, recipes)
  UI/HUD.cs, MenuScreens.cs, TutorialHints.cs, OrderBubble.cs, UpgradeCard.cs, ButtonFeel.cs
  Audio/SFX.cs               every sound effect goes through here (variants, pitch jitter, gameplay-driven loops)
  Audio/MusicDirector.cs     adaptive music synthesized live, layered by how busy the bakery is
  FX/FX.cs, WorldBar.cs, BakeryAmbience.cs   floating text, puffs, hearts, bars, sky color by time of day
  Debug/AutoPlayer.cs        a bot that plays the game by itself (used for testing and balancing)
Assets/Editor
  BakeryBuilder.cs           builds the scene, prefabs, fonts and UI
  BakeryAssetImporter.cs     pixel-art import settings for everything in Assets/Art
Tools
  prepare_assets.py          cuts the sprites out of the downloaded packs, draws the custom ones, trims the sounds
  build.sh                   rebuilds the scene and the Mac app from the command line
```

### Testing with the bot

`AutoPlayer` plays through all three days on its own and saves screenshots. It only runs when the game is started with `-autoplay`:

```
"Builds/Mac/Enzos Bakery.app/Contents/MacOS/Enzo's Bakery" -autoplay -shots ~/Desktop/shots -shotEvery 5 -timeScale 2
```

Each day's results are written to the player log.
