# RollOfTheScout — Class Introduction · 职业介绍

**English** ｜ **中文**（见下方）

---

## English

Class introduction — applies to the **RollOfTheScout** mod, version **1.0.0** and above.

1. **Explorer**
- Keywords: Random items each level · One-time faint revive · High hunger drain · Periodic infinite sprint
    - "Pioneer: a random-item backpack every level; your first faint fully clears debuffs as you revive; 【Hyper-metabolism】 forces you to keep managing hunger and sprints."
- 【Trailblazing】On entering each new level, automatically equips a backpack filled with 4 items; if you already carry a backpack, the old backpack and its contents are overwritten. The 4 items are drawn independently by weight from rope (weight 10; same below), piton (7), scout cookies (2), chain shooter (2), rescue hook (2) and anti-gravity cannon (1); duplicates are allowed.
- 【Unbroken】On fainting, immediately clears all debuffs; once per run (independently per player).
- 【Hyper-metabolism】Every 20 s, a fixed +2.5 hunger, which replaces the game's default natural hunger growth; every 40 s, one sprint (about 10 s — i.e. an energy drink, and when it ends 25 drowsiness is cleared).
2. **Leader**
- Keywords: Area buffs · Opening debt · Bound to everyone's life · Death-triggered rally · Extra mystery item
    - "Team core: strong sustained area buffs; your curse and petrify swing with everyone's life and death; your death rallies everyone; while all are alive, opening an Ancient Idol yourself yields an extra item."
- 【Rally】Every 3 min, all players within 30 m (including yourself) gain +25 extra stamina, −5 injury, −10 hunger; every 1 min, all players within 20 m (including yourself) gain −30 drowsiness.
- 【Revelation】While everyone is alive, opening an Ancient Idol additionally yields 1 mystery item; the item is placed into an empty slot of the Leader's inventory first, and when all 3 slots are full it is spawned next to the campfire instead, so it cannot roll down the mountain.
- 【Holy】Whenever any player opens an Ancient Idol, the Leader's state is adjusted to the number of players present: for each death, the Leader gains +5 curse and +10 petrify (inherited); for each living player, the Leader gains −2.5 curse (by default the Leader themself is not counted as a living player).
- 【Fate】Starts with 20 curse, 20 petrify and 30 extra stamina.
- 【Morale】When the Leader dies, every still-living player immediately gains +10 curse, +10 petrify, +30 extra stamina and one sprint (about 10 s); when the Leader revives, those players gain −10 curse and −10 petrify (debuffs removed).
3. **Logistician**
- Keywords: Periodic random supply · Starting backpack · High starting petrify
    - "Mobile supply: starts with a random backpack; receives random supplies on a timer; your starting curse and high petrify need careful management; special bond with the Cook."
- 【Emergency Airdrop】By default, 1 random item every 90 s; nothing is dropped while all 3 inventory slots are full.
(default item pool: rope, piton, lantern, torch, sun umbrella, flare, rescue hook, glider, auto parachute, rope gun, chain shooter, portable stove, balloon, bundle of balloons, antidote, aloe, heat pack, sunscreen, bandage, medicinal root, first-aid kit, scout cookies, energy drink, sports drink, compass, pirate compass, travel guide, telescope, candle, frisbee, stone, scout cannon, bingbong)
- 【Well-prepared】At the start, by default a 50% chance to be equipped with a backpack and a 50% chance with a jetpack.
- 【Buildup】Starts with 10 curse and 70 petrify; reviving returns you to 70 petrify.
4. **Doctor**
- Keywords: Medical supplies each level · Area injury/poison relief · Self-injury on teammate death
    - "Field medic: a random medical backpack every level; sustained close-range injury and poison relief; a teammate's death triggers a self-injury penalty."
- 【Portable Medical】On entering each new level, automatically equips a backpack filled with 4 items; if you already carry a backpack, the old backpack and its contents are overwritten. The items are drawn independently by weight from bandage (8), medicinal root (6), elixir mushroom (5), medkit (5) and panacea (1); duplicates are allowed.
- 【Nursing】Every 8 s, all players within 5 m gain −2.5 injury; the Doctor's own reduction defaults to a multiplier of 1, i.e. the same as teammates.
- 【Toxicology】Every 3 s, all players within 5 m gain −2.5 poison and −2.5 spores; the Doctor's own reduction defaults to a multiplier of 1, i.e. the same as teammates.
- 【Empathy】For each teammate death, the Doctor themself takes 10 injury (this effect has a 5 s cooldown: at most one injury from it per 5 s — for 5 s after an injury is detected, no further injury is taken from this effect).
5. **Assassin**
- Keywords: Starting weapons · Dagger and blowgun · Pickup conversion
    - "A matter of life and death: starts with a ritual dagger and a blowgun; picked-up items may become weapons; to kill or to save, that is the question."
- 【Assassination】Starts with 1 ritual dagger and 1 blowgun; picking up any item has a 10% chance to become a ritual dagger (weight 1) or a blowgun (weight 1) (by default some special items are exempt: glow mushroom, ritual dagger, blowgun, signal stick, strange crystal, Scout's perseverance, Scout's initiative, Scout's hospitality, Scout's ambition, Scout's glory).
6. **Pila · Pandora**
- Keywords: Area Pandora · Pickup conversion · Chance to vanish · Steadily rising curse
    - "Child of chaos: keeps spreading the Pandora effect around you; half of the items you pick up become boxes and half vanish; your curse keeps stacking."
- 【Pandora】Every 5 s, applies the Pandora's Box effect to all players within 5 m (by default not including yourself); every 30 s, +5 curse to yourself; picking up any item has a 50% chance to become a Pandora's Box and a 50% chance to simply vanish (by default some special items are exempt: glow mushroom, Pandora's Box, signal stick, strange crystal, Scout's perseverance, Scout's initiative, Scout's hospitality, Scout's ambition, Scout's glory).
7. **Necromancer**
- Keywords: Book of Bones · Death buffs · Skeleton empowerment · High starting curse · Curse/gain management
    - "King of skeletons: a tribute to the mod's least small group; lower your curse through player deaths and skeleton allies; keep poisoning nearby humans while skeletons gain extra stamina and invincibility."
- 【Bones】Starts with 50 curse; on entering each new level, gains 1 Book of Bones (placed into an empty slot first, otherwise spawned at your feet); picked-up Pandora's Boxes turn directly into Books of Bones.
- 【Undead】For each player death, you gain −5 curse and +20 extra stamina; whenever a player revives as a human, you gain +5 curse; whenever a player revives as a skeleton, you gain −10 curse; for each skeleton player on the field, every 1 min you gain +15 extra stamina and every 3 min −2.5 curse; every 5 s, human players within 15 m (excluding yourself) gain +2.5 poison, skeleton players gain +2.5 extra stamina, and they gain one invincibility (milk-white gold) every 10 s.
8. **Savage**
- Keywords: Poison/spore resistance · Natural food only · Periodic pink banana · Tools disabled
    - "Power of nature: strong resistance to poison and spores; you can only eat raw natural food or take injury; gains a pink banana on a timer; climbing tools are unusable — picked-up ones turn into wooden sticks."
- 【Natural Tenacity】Every 5 s, −2.5 poison and −2.5 spores.
- 【Wild Instinct】You can only eat natural food; eating packaged items (except energy drink, milk-white gold and the big lollipop) or cooked food (by default including natural food) gives +5 injury.
- 【?Banana?】 Every 1 min, a fixed 1 pink banana (given to an empty slot first, otherwise spawned directly at your feet).
- 【Knowledge Corruption】Picking up a piton, rope gun, chain shooter, rescue hook or anti-gravity cannon turns the item directly into a wooden stick.
9. **M**
- Keywords: Debuff conversion · Penalty without debuffs · Periodic debuff tolerance
    - "Negative conversion: most debuffs you take convert into an equal amount of extra stamina; with no debuffs you periodically lose extra stamina; tolerant against specific debuffs."
- 【Enjoy】When you take injury, poison, spores, thorns, heat, cold, cobweb, arrow or flytrap, you gain an equal amount (default multiplier 1) of extra stamina; every 60 s your state is checked, and if you have none of those specific debuffs (injury, poison, spores, thorns, heat, cold, cobweb, arrow, flytrap), you lose 10 extra stamina.
- 【Tolerance】Every 10 s, −5 of the specific debuffs (injury, poison, spores, thorns, heat, cold).
10. **Scout**
- Keywords: Starting random fanny pack · Solo bleeding · Group action · Companion buffs
    - "Better together: starts with a random-item fanny pack; alone you bleed and starve; you need company to ease it; strengthened bond with fellow Scouts and Leaders."
- 【Luggage】Starts by default with a fanny pack containing 2 random items (distinct).
(item pool: scout cookies, big lollipop, airplane meal, hot dog sausage, marshmallow, mixed nuts, oatmeal bar, yellow snowberry, red crispberry, yellow banana berry, tiny egg, energy drink, sports drink, rope, milk-white gold, bandage, first-aid kit, balloon, bunch of balloons, glider, auto parachute, sun umbrella, glow stick, lantern, pirate compass, telescope, basketball, compass, frisbee, torch, stone, passport, bingbong)
- 【Collective】By default, every 20 s +2.5 injury and +2.5 hunger; when another player (with no Scout or Leader) is within 15 m, every 20 s −2.5 injury and every 50 s −2.5 hunger; when another Scout or Leader is within 15 m, every 15 s −2.5 injury and every 30 s −2.5 hunger.
11. **StandardBearer**
- Keywords: Checkpoint flag · Area debuff purge on plant · Self petrify on plant · Random team buffs · Team burst
    - "Team standard-bearer: a checkpoint flag every level; planting it immediately clears debuffs in range and 3 s later grants everyone random buffs; each plant adds petrify to yourself."
- 【Planting the Flag】On entering each new level, automatically gains a checkpoint flag (placed into an empty slot first, otherwise spawned directly at your feet); when you use a checkpoint flag (plant it), all players within 15 m (by default including yourself) immediately have their debuffs cleared (curse and petrify excluded, and the StandardBearer themself gains +20 petrify), and 3 s later all living players (including yourself) randomly gain about 10 s of infinite stamina (big lollipop, effect weight 1; same below), about 10 s of sprint (energy drink, 1) and/or about 10 s of invincibility (milk-white gold, 1) — these three can stack, i.e. at least 1 and by default at most 3 of them.
12. **Dancer**
- Keywords: Dance for stamina · Forced group dance · Periodic backflip · Fall risk
    - "Dance together: players dancing in your range grant extra stamina; your own dance forces nearby players to dance; periodic forced backflips that always end in a fall."
- 【Dance Fever】Within 15 m of you, for each player who performs the dance emote, you and that player gain +2.5 extra stamina (once detected, by default there is a 1 s detection cooldown).
- 【Showstopper】When you perform the dance emote, players within 15 m are forced to dance.
- 【Fanatical Dancer】Every 120 s you are forced into one backflip (and you always fall over after the backflip).
13. **Daihao**
- Keywords: Periodic eggs · Contagious reclass · Same-class area healing · Periodic backflip
    - "Meme spread: voice and egg cycles grant extra stamina; contagious reclassing — you can force-merge others by feeding fried eggs; same-class players heal and take less injury over a larger range, but periodic backflips always end in a fall."
- 【Incarnation】Your skin becomes the Daihao look for this run.
- 【Can You Hear Me?】Every 60 s your voice state is checked; if you are speaking, +10 extra stamina.
- 【Slippery Feet】By default, every 150 s you are forced into one backflip (and you always fall over after the backflip).
- 【Laying】Every 120 s you gain a big egg (placed into an empty slot first, otherwise spawned at your feet); while a big egg is in your inventory, +5 extra stamina every 60 s.
- 【Meme Contamination】While a Daihao is present in the run, other players who eat a big egg / fried egg are reclassed directly into Daihao.
- 【All of Us】For every Daihao other than yourself within 50 m, −2.5 injury every 30 s.
14. **Saint**
- Keywords: Items disabled · Pickups vanish · Periodic reset · Amplified injury · Class immunity
    - "Input text here: food and most items are disabled and picked-up items vanish; hunger resets to zero every level; extra stamina refills periodically; any injury is amplified into a knockdown; immune to the Pandora, Necromancer, Dancer and Qu class effects."
- 【Teaching】Players within 10 m (excluding yourself) gain +2.5 extra stamina and +10 drowsiness every 10 s.
- 【Perfectionism】Every 60 s, +100 extra stamina; picked-up items vanish 100% of the time (by default some special items are exempt: glow mushroom, signal stick, strange crystal, Scout's perseverance, Scout's initiative, Scout's hospitality, Scout's ambition, Scout's glory).
- 【Fasting】On entering each new level, −100 hunger and one invincibility (milk-white gold).
- 【Fallen Idol】Every 1 s, if injury is detected on yourself, +100 injury.
- 【Detached】Unaffected by the effects of the Pandora, Necromancer, Dancer and Qu classes.
15. **Mage**
- Keywords: Periodic scrolls · Two forms · Random effects · Negative cost
    - "Random casting: scrolls on a timer; opening one triggers a random effect and adds petrify/curse; the human form leans support, the skeleton form trades a high cost for high gain."
- 【Magic Scroll】Every 3 min you gain 1 scroll (placed into an empty slot first, otherwise spawned directly at your feet); (if you are human) opening a scroll immediately gives you +5 petrify and, 3 s later, randomly triggers 1 of the following effects:
    - 【Empower】Players within 15 m (including yourself) gain one sprint (energy drink).
    - 【Excite】Players within 15 m (including yourself) gain one infinite stamina (big lollipop).
    - 【Harden】Players within 15 m (including yourself) gain one invincibility (milk-white gold).
    - 【Enlighten】Players within 10 m (excluding yourself) gain +50 extra stamina.
    - 【Purify】For the next 5 s, players within 10 m (including yourself) gain −2.5 debuffs per second.
    - 【Heal】For the next 10 s, players within 15 m (including yourself) gain −2.5 injury per second.
    - 【Anti-Gravity】For the next 10 s, players within 20 m (including yourself) spawn 1 anti-gravity field at their feet every 5 s.
    - 【Teleport】Teleports players within 10 m (including yourself) to a random player 50 m away from you.
    - 【Resurrect】Randomly revives 1 player (next to you) (Scout statue); if there is no dead player to revive, you instead clear all your own debuffs.
- 【Black Magic】If you are a skeleton, opening a scroll immediately gives you +5 curse and triggers the scroll effect immediately; the scroll's possible effects are replaced by the following, and each time more than one can randomly trigger (i.e. at least 1, at most 2, with a 1 s gap, and the default chance to chain-trigger one more effect is 30%):
    - 【Blood Sacrifice】Human players within 15 m gain +10 injury, +20 extra stamina and one sprint; for each player affected by this, you gain +2.5 curse and +25 extra stamina; you gain one sprint and one invincibility.
    - 【Offering】A random 1 player within 15 m (excluding yourself) is hit with a cursed skull; you gain one sprint + infinite stamina + invincibility and +5 curse.
    - 【Swap】Swap positions with a random player 50 m away from you; if no player qualifies, you gain 1 quji mushroom.
    - 【Tornado】A tornado is spawned at a random player's feet (it lasts only 5 s); you gain one invincibility and 2 bunches of balloons tied onto you.
    - 【Beetles】1 beetle spawns above the heads of players within 15 m (including yourself); you gain 1 beehive.
    - 【Zombify】A random 1 human player within 15 m (excluding yourself) gains +100 spores, and you gain 1 mandrake and +2.5 curse; if no player qualifies, a zombie spawns 3 m away from you instead.
    - 【Soul Return】Randomly revives 1 player (next to you) and that player becomes a skeleton (Book of Bones); if there is no dead player to revive, a Scoutmaster spawns 10 m away from you (chasing for 15 s).
    - 【Backlash】Players within 15 m (including yourself) gain +10 curse and +10 petrify.
16. **Challenger**
- Keywords: High starting debuffs · Golden grapnel · Heavy grab penalty · Pandora immunity
    - "Aerial mobility: high starting curse and petrify need careful management; starts with a golden grapnel and a glider; hooking a player with the grapnel immediately triggers a heavy injury penalty; immune to Pandora effects and picked-up Pandora lunchboxes vanish."
- 【Extreme】Starts by default with 60 curse and 70 petrify; after dying and reviving you return to 60 curse and 70 petrify.
- 【Top Player】Starts with a golden grapnel and a glider; unaffected by the Pandora's 【Pandora】 effect.
- 【Disdain】When your grapnel hooks a player, you immediately faint (+50 injury); picked-up Pandora's Boxes vanish directly.
17. **MasterChef**
- Keywords: Portable stove · Cooking sates hunger · Cooking builds poison · Non-food rewards · Heat while holding
    - "Grill master: portable stoves on a timer; cooking anything sates hunger; cooking too much poisons you; periodically cooking non-food (tools) grants extra stamina; holding a portable stove causes frequent heat."
- 【Portable Stove】Starts with 1 portable stove, and gains 1 portable stove every 5 min.
- 【Hand Warmer】While holding a portable stove, +2.5 heat every 1 s.
- 【Cooking】For each item cooked in this run, −2.5 hunger.
- 【Gas Poisoning】For every 3 items cooked in this run, +10 poison.
- 【Demolition Master】Within each 1 min, cooking a non-food item triggers a one-off +10 extra stamina.
18. **Cook**
- Keywords: Portable stove · Cooking raw food · Group buffs · Cooked-food bonus · Raw-food penalty · Hunger-induced drowsiness
    - "Food security: portable stoves on a timer; cooking raw food restores hunger and stamina around you; cooked food pays extra, raw food punishes hard; nearby hungry players make you drowsy; special bond with the Logistician."
- 【Logistics Support】On entering each new level, automatically gains 1 portable stove; every 3 min gains one portable stove (placed into an empty slot first, otherwise spawned directly at your feet); while a Logistician is within 30 m, a random food is gained every 90 s — nothing is given while all 3 inventory slots are full, and it is never forced into your main hand (cf. the Logistician's 【Emergency Airdrop】).
- 【Food Supply】Each time you cook an (uncooked) food item, all players within 15 m (including yourself) gain −5 hunger and +5 extra stamina.
- 【Food Safety】Each time you eat cooked food, additionally −2.5 hunger, −2.5 injury and +10 extra stamina.
- 【Traumatic Stress】Each time you eat uncooked food (except glow mushroom, energy drink, milk-white gold and the big lollipop), +10 injury, +20 poison and +30 drowsiness; while a player with hunger > 30 (including yourself) is within 15 m, +5 drowsiness every 3 s.
19. **Pacifist**
- Keywords: Kick replaces grab · Hits restore stamina · Misses cause drowsiness · Starting curse
    - "Not so peaceful: the right-click grab becomes a kick; kicking a player grants extra stamina, missing adds drowsiness; starts with curse."
- 【Peace?】Starts with 20 curse; your right-click grab becomes a kick; each time you kick a player you gain +2.5 extra stamina, and if the kick misses a player you lose −2.5 extra stamina and gain +20 drowsiness.
20. **Hamster**
- Keywords: Starting backpack · Item hoarding · Load reward · Periodic sprint · Extra stamina
    - "Carrying the weight: starts with a backpack; once your load passes the threshold you periodically gain a sprint and extra stamina."
- 【I Want It All】Starts by default with a backpack; while your load is ≥ 50, you gain one sprint every 10 s and +5 extra stamina every 20 s.
21. **Firefighter**
- Keywords: Faint rescue · Teleport invincibility · Post-rescue sprint · Carrying heals both · Death penalty
    - "Fireline rescue: a teammate's faint auto-teleports you with invincibility; picking them up grants a sprint and a large amount of extra stamina; carrying a player heals both; a player's death gives you petrify."
- 【Warm Embrace】While carrying a player, every 20 s you and the player you are carrying take −2.5 injury.
- 【Fireline Rescue】Whenever a player faints, 3 s later you (the Firefighter) gain one invincibility and teleport to the fainted player; after picking that fainted player up you (yourself) immediately gain one sprint and +50 extra stamina; this effect by default has a 10 s trigger cooldown and cannot trigger while you are carrying a player.
- 【Dutiful】For each human player death, +15 petrify to yourself (this effect by default has a 30 s cooldown).
22. **Narcissist**
- Keywords: Sunscreen · Refreshed each level · Forced overwrite · Sunscreen stamina · Per-bottle bonus
    - "Exquisite sunscreen: three sunscreens every level, force-overwriting your inventory; gaining sunscreen grants extra stamina, and finishing each bottle grants more extra stamina plus petrify."
- 【Vanity Case】On entering each new level, automatically gains 3 sunscreens (forcibly overwriting the 3 inventory slots).
- 【Refinement】Starts with 10 curse; each time you gain the (sunscreen) sunscreen status, +20 extra stamina (this effect by default has a 5 s cooldown); each time you finish a bottle of sunscreen, +30 extra stamina.
- 【Chemical Poisoning】Each time you finish a bottle of sunscreen, +10 petrify.
23. **LoneWolf**
- Keywords: Solo reward · Company penalty · Radius check
    - "Single-player mode: periodically rewarded with extra stamina when far from others; constant drowsiness when someone is close."
- 【Lone Trail】Every 30 s, if no other player is within 50 m, +10 extra stamina; while another player is within 15 m, +2.5 drowsiness every 3 s.
24. **Gunner**
- Keywords: Scout cannon · Each level and periodic · Injury dynamite · Main-hand risk
    - "Heavy gunner: a scout cannon every level; scout cannons on a timer; taking damage grants dynamite, which is forced into your main hand when the inventory is full."
- 【Fire!】On entering each new level, automatically gains 1 scout cannon; gains one scout cannon every 3 min.
- 【Blowback】Each time you take damage (injury increases), you immediately gain one dynamite (placed into an empty slot first; if the inventory is full it is forced into your main hand).
25. **Band**
- Keywords: Horns and instruments · Performance area buff · Pickup conversion · Inventory bonus · Starting curse · Special reclass
    - "The playing band: picked-up items may become instruments; inventory and performance grant area extra stamina; holding all three instruments grants an extra sprint, and giving them to others has a low chance to reclass them."
- 【Instruments】Picking up an item other than a food item by default has a 50% chance to stay unchanged, a 30% chance to become a trumpet, a 10% chance to become the Scoutmaster's horn and a 10% chance to become a friendship horn (besides food items, by default some special items are exempt: glow mushroom, trumpet, Scoutmaster's horn, friendship horn, signal stick, strange crystal, Scout's perseverance, Scout's initiative, Scout's hospitality, Scout's ambition, Scout's glory); by default every 30 s your inventory is checked and, within it, each trumpet gives +2.5 extra stamina, each Scoutmaster's horn gives +5 extra stamina and each friendship horn gives +5 extra stamina; if all three of the above are held at once, you also gain one extra sprint.
- 【Performance】Each time you blow a trumpet, all players within 15 m (excluding yourself) gain +2.5 extra stamina (by default with a 1 s cooldown; same below); each time you blow the Scoutmaster's horn, you gain +2.5 extra stamina; every 4 instruments blown (trumpet, Scoutmaster's horn, friendship horn) give you +5 drowsiness.
- 【The Band】Every 30 s, for each band member other than yourself within 15 m, you gain +5 extra stamina and −30 drowsiness, and the radius of the 【Performance】 effect is widened by +5 m (up to +20 m) (i.e. by default the check runs every 30 s and, if the conditions hold, the effect triggers and stays active for 30 s; otherwise it is inactive until the next check); when a player (other than a band member) has a trumpet, Scoutmaster's horn or friendship horn in their inventory and has been within 5 m of a band member for more than 30 s, that player by default has a 10% chance to be reclassed into a band member immediately (default cap of 5 players in total).
- 【Frail】Starts with 20 curse.
26. **Scoutmaster · MilesSoul**
- Keywords: Scout gems · Locked curse · Pickups vanish · Special bonuses · Rule Zero · Nadir liberation
    - "Scoutmaster's soul: starting curse is locked and only Scout gem items can lower it; Scout gems grant exclusive bonuses; all non-gem items are disabled and picked-up items vanish; instant teleport rescue, never abandon others; collecting every Scout gem grants an extra reward; freeing the soul at the Nadir rewards everyone."
- 【Cursed Soul】Starts with 15 curse; by default your curse cannot be reduced by any means other than your own class effects and the Mage's 【Magic Scroll】 sub-effect 【Purify】 (e.g. being fed a cooked mandrake, being revived through a Scout statue, being fed a Pandora lunchbox, etc.; on entering each new level and after triggering any class effect that would reduce your curse, your current curse is immediately recorded, and whenever your curse is detected to have decreased for a reason other than your own class effects and 【Purify】, it is forcibly restored to the latest recorded value), and you are unaffected by the Pandora's 【Pandora】 effect.
(You cannot become this class after the beach)
- 【Hindrance of the Souls】On entering each new level, +5 curse, until the Peak (entering the Peak and the Nadir does not add curse); every 13 uses of any Scout gem item (Scout's perseverance, Scout's initiative, Scout's hospitality, Scout's ambition), a Scoutmaster spawns more than 30 m away from you (chasing for 30 s).
- 【Seal's Power】Except for some special items, you cannot pick up any item (picked-up items vanish 100% of the time) (by default some special items are exempt: glow mushroom, backpack, fanny pack, signal stick, strange crystal, Scout's perseverance, Scout's initiative, Scout's hospitality, Scout's ambition, Scout's glory); picking up and using Scout gem items has special effects:
    - 【Scout's Perseverance】Each use (on yourself) immediately gives −10 petrify and −10 hunger; the first pickup in this run gives −5 curse.
    - 【Scout's Initiative】Each use immediately gives −5 petrify, −5 hunger and +5 extra stamina; the first pickup in this run gives −5 curse.
    - 【Scout's Hospitality】Each use (that successfully copies an item) immediately gives −20 petrify, −10 hunger and +20 extra stamina; the first pickup in this run gives −5 curse.
    - 【Scout's Ambition】Each use immediately gives −25 petrify, −10 hunger and +25 extra stamina; the first pickup in this run gives −5 curse.
    - 【Scout's Strength】After all four of the above Scout gem items have been picked up, an additional −5 curse, all debuffs other than curse are cleared (petrify included), and you immediately gain one invincibility and one sprint.
- 【Rule Zero】Every 30 s your player count within 30 m (excluding yourself) is checked: for each player, +5 extra stamina; each time you use Scout's perseverance on another fainted player, your petrify does not increase (the petrify that would have been added by that use is immediately cleared by the same amount, and you gain an extra +30 extra stamina), and that use does not count towards 【Hindrance of the Souls】' tally; after you have picked up Scout's perseverance, each time a player faints you immediately gain one invincibility and teleport to the fainted player, and this effect by default has a 10 s trigger cooldown; for each player death, +10 injury to yourself.
- 【Seal Released】After entering the Nadir and (any player) freeing the Scoutmaster's soul, all debuffs (curse and petrify included) of every player present (including yourself) are immediately cleared, and +100 extra stamina.
27. **Qu**
- Keywords: Early bug · Frequent climbing slips · Decomposing junk rewards · Contagious reclass · Same-class buffs · Spore immunity · Bug-eating penalty
    - "Wriggling Qu: frequent forced let-go while climbing; profit from decomposing junk items and accumulating early bugs; contagious reclassing — periodically applies spores nearby, and past the threshold forces others to merge and grants early bugs; same-class clusters get sprint and infinite stamina rewards; immune to spores; eating early bugs costs curse."
- 【?! Qu?!】By default, while climbing you are forced to let go once every 0.5 s (it does not trigger on vines, ropes, anti-gravity ropes, pitons or chains); on death, a cooked early bug spawns at your corpse;
- 【Decomposer】Picking up some items makes them vanish directly and gives you −5 hunger.
(default item pool: the four banana peels, wooden stick, torch, the three poisonous mushrooms, bouncy mushroom, pedal mushroom, cloud mushroom, quji mushroom, elixir mushroom, cactus, magic bean, travel guide, scroll, torn page, Book of Bones)
- 【Blockchain】Every 5 s you gain −100 spores; every 7 s, non-Qu players within 10 m gain +2.5 spores (multiple Qu do not stack); each time you feed another player an early bug, the fed player gains +15 spores; players with spores ≥ 50 that are within 10 m of a Qu are forcibly reclassed into Qu, and all Qu gain one early bug.
- 【Lots of Qu】For every 5 【Decomposer】 triggers, you gain one early bug (placed into an empty slot first, otherwise spawned at your feet); by default every 60 s, your inventory and players within 30 m are checked: for each early bug in your inventory or each Qu (same class, excluding yourself) within 30 m, you gain −2.5 hunger and +2.5 extra stamina (by default stacking up to 4 layers); if there are 3 early bugs in your inventory or the number of Qu within 20 m is ≥ 3 (including yourself), you additionally gain one sprint; if the number of Qu within 20 m is ≥ 4 (including yourself), you additionally gain one infinite stamina.
- 【Qu Poison Spares Its Own】Each time you eat one early bug (cooked or not), +10 curse.
28. **Astronaut**
- Keywords: Periodic balloons · Pickup conversion · Packaged/cooked food only · Heat/cold resistance · Extra debuffs
    - "Space drift: balloons are tied on periodically; balloons and bunches of balloons cannot be picked up; picked-up items may become rocket packs or jetpacks; only packaged or cooked food, otherwise poison; some heat/cold resistance but certain debuffs hit harder."
- 【Weightless】At the start you have 2 balloons tied on, and every 2 min 2 more balloons are tied on; picking up a balloon or a bunch of balloons makes it vanish directly.
- 【Fly Away】Picking up an item other than food items and some special items has a 20% chance to become a rocket pack and a 10% chance to become a jetpack (when the roll succeeds, the picked-up item vanishes and a rocket pack or jetpack spawns at your feet) (by default some special items are exempt: glow mushroom, rocket pack, jetpack, signal stick, strange crystal, Scout's perseverance, Scout's initiative, Scout's hospitality, Scout's ambition, Scout's glory).
- 【Space Food Safety】You can only eat packaged food and cooked food; each time you eat a food that does not qualify, +10 poison (glow mushroom and panacea excluded).
- 【Wow, NASA Really Is Great】Every 5 s, −5 heat and −5 cold; each time you gain (an increase of) cobweb, flytrap or curse, an additional +2.5 of the corresponding value.
29. **Langwang**
- Keywords: Periodic kingberries · Eating rewards · Non-berry penalty · Injury per meal · Special reclass
    - "Big-berry addiction: green/purple kingberries on a timer; green grants a sprint, purple grants infinite stamina; other food adds drowsiness; every meal costs injury; others who chain-eat kingberries are forced to merge."
- 【Premium Big Berry】(Two sips!) Every 100 s, you gain a green kingberry (by default item weight 10; same below) or a purple kingberry (5) (placed into an empty slot first, otherwise spawned directly at your feet); each time you eat a green kingberry you gain one sprint; eating a purple kingberry gives you one infinite stamina.
- 【Addiction】(This is weak) Each time you eat food other than the various kingberries, +15 drowsiness.
- 【Fibrosis】(Moment of silence) Each time you eat an item, +2.5 injury (except glow mushroom, panacea, energy drink, milk-white gold and the big lollipop).
- 【Quit While You Can】(You too) While a Langwang is present in the run, if another player eats any kingberry more than three times in a row (streak > 3), that player is forcibly reclassed into Langwang.
30. **Programmer**
- Keywords: Periodic insects · Extra stamina overflow modulo · Night sprint · Solo debuff cleanse · Class-effect copy
    - "Power of code: random insects on a timer, usually with poison; extra stamina overflow wraps around; periodic sprint at night; solo play periodically cleanses debuffs; with no debuffs and no insects you copy another present class's effect for a while."
- 【BUG】Every 180 s you gain a random insect (tick 70%, scorpion 20%, early bug 10%; placed into an empty slot first, otherwise nothing is given); if it is a tick, you immediately gain +10 poison; if it is a scorpion, you immediately gain +25 poison.
- 【Overflow】The more extra stamina you have, the less extra stamina you have: whenever your extra stamina increases, if (current extra stamina + this increase) > 100, your extra stamina becomes (current extra stamina + increase) mod 100 (e.g. when you have 95 extra stamina and eat a food that would add 10 extra stamina, your extra stamina becomes 5).
- 【Night Owl】Every 60 s a check runs: if it is night, you gain one energy drink effect, and when it ends the drowsiness it caused is not additionally cleared.
- 【Cheat】Every 10 s a check runs: if no other player is within 30 m, one random debuff of yours is reduced by −2.5 (injury, spores, poison, sleep, cold, heat included); every 3 min a check runs: if you have no debuff (injury, spores, poison, sleep, cold, heat) and no insect BUG (tick, scorpion, early bug) in your inventory, you gain one random non-debuff class effect of a random player present, which stays active for 3 min until the next check.

---

## 中文

职业介绍·适用于模组童子军的命运之骰RollOfTheScout的v1.0.0版本及以上：
1. **先驱**
- 关键词：每关随机道具、一次性昏迷复活、高饥饿消耗、无限周期加速
    - “开路先锋：每关获得随机道具背包；首次昏迷将完全清除负面复活；【高代谢】迫使你不断管理饥饿与冲刺”
- 【开路】每次进入新关卡时，自动装备一个装满4件物品的背包；如果原本背着背包，旧背包连同内容物会一起被覆盖。背包内物品在绳索（权重10，后同）、岩钉（7）、童军饼干（2）、锁链发射器（2）、救援抓钩（2）、反重力炮（1）中按各自权重独立随机抽取，允许重复
- 【不屈】昏迷时立即清除所有负面状态，每局仅一次（各玩家独立）
- 【高代谢】每20秒固定+2.5饥饿值，并取代游戏默认的自然饥饿增长；每40秒获得一次加速，加速时长约10秒（即能量饮料，且加速结束时会清除掉25困倦）
2. **领袖**
- 关键词：范围增益、开局负债、绑定全员生死、亡语激励、额外神秘物品
    - “团队核心：持续强力范围增益；自身诅咒石化随全员生死波动；自身死亡触发全员激励；全员存活时，自己开启雕像可获额外物品”
- 【号召】每3分钟为半径30米内的所有玩家（包括自己）+25额外体力、-5损伤、-10饥饿；每1分钟为半径20米内所有玩家（包括自己）-30困倦
- 【神启】全员存活时开启古老雕像可额外获得1件神秘物品；物品会优先放进领袖的物品栏空位，当3个物品栏都满时改为在篝火旁固定生成，避免滚落山下
- 【圣洁】任何玩家开启古老雕像时，都会按当时人数情况调整领袖的状态：每死亡1人，领袖+5诅咒、+10石化（会继承）；每有1人存活，领袖-2.5诅咒（存活人数默认配置不把领袖本人算在内）
- 【宿命】开局初始自带20诅咒、20石化、30额外体力
- 【士气】领袖死亡时，所有仍存活的玩家立刻+10诅咒、+10石化、+30额外体力，并获得一次（约10秒）的加速效果；领袖复活时，这些玩家-10诅咒、-10石化（负面消除）
3. **后勤**
- 关键词：周期随机补给、开局背包、初始高石化
    - “移动补给：开局自带随机背包；定时获得随机物资；初始诅咒与较高石化需谨慎管理；与【炊事员】有特殊羁绊”
- 【紧急空投】默认每90秒获得1件随机物品；三个物品栏都满时不发放
（默认物品池：绳索，岩钉，提灯，火把，遮阳伞，信号弹，救援抓钩，滑翔伞，自动降落伞，绳索枪，链索发射器，便携炉灶，气球，气球束，解毒剂，芦荟，暖宝宝，防晒霜，绷带，药用根茎，急救包，童军饼干、能量饮料、运动饮料、罗盘、海盗罗盘、旅行指南、望远镜、蜡烛、飞盘、石头、童子军大炮、宾邦）
- 【备战充分】开局初始默认50%概率装备一个背包，50%概率装备一个喷气背包
- 【积损】开局初始自带10诅咒、70石化；复活会回到70石化
4. **医生**
- 关键词：每关医疗物资、范围减伤解毒、队友死亡自伤惩罚
    - “随队医护：每关获得随机医疗物资背包；近身小范围持续减伤解毒；队友阵亡会触发自伤惩罚”
- 【随身医疗】每次进入新关卡时，自动装备一个装满4件物品的背包；如果原本背着背包，旧背包连同内容物会一起被覆盖。背包内物品在绷带（8）、药用根茎（6）、灵药菇（5）、医疗箱（5）、万灵药（1）中按权重独立随机抽取，允许重复
- 【护理学】每8秒对半径5米内的所有玩家-2.5损伤，对医生自己的削减量默认倍率为1，即与队友相同
- 【毒理学】每3秒对半径5米内的所有玩家-2.5中毒、-2.5孢子，对医生自己的削减量默认倍率为1，即与队友相同
- 【共情】每有队友死亡时，医生自身受到10损伤（本效果存在5秒冷却：每5秒最多因本效果受伤一次——检测到损伤后5秒内不会再受到本效果损伤）
5. **刺客**
- 关键词：初始武器、匕首吹箭、拾取转化
    - “生死问题：开局自带仪式匕首与吹箭筒；拾取物品概率变为武器；杀人还是救人，这是一个问题”
- 【刺杀】开局初始自带1把仪式匕首、1个吹箭筒；拾取起任何物品都有10%概率变成仪式匕首（权重1）或吹箭筒（权重1）（默认部分特殊物品不受影响：诡异菇、仪式匕首、吹箭筒、信号棒、奇怪水晶、童军的毅力、童军的进取、童军的盛情、童军的野心、童军的荣耀）
6. **皮拉·潘多拉之子**
- 关键词：范围潘多拉、拾取转化、概率消失、持续自增诅咒
    - “混沌之子：持续向周围散播潘多拉效果；拾取物品半变魔盒半消失；自身诅咒持续叠加”
- 【潘多拉】每5秒向半径5米内的所有玩家施加潘多拉魔盒效果（默认不包括自己）；每30秒自身+5诅咒；拾取任何物品都有50%概率变成潘多拉魔盒，50%概率物品直接消失（默认部分特殊物品不受影响：诡异菇、潘多拉魔盒、信号棒、奇怪水晶、童军的毅力、童军的进取、童军的盛情、童军的野心、童军的荣耀）
7. **死灵法师**
- 关键词：骸骨之书、死亡增益、骷髅强化、初始高诅咒、诅咒与增益运营
    - “骷髅之王：致敬本模组最不小团体的职业；通过玩家死亡、与骷髅配合降低自身诅咒；持续为周围的人类玩家施加中毒、骷髅玩家获得额外体力与无敌”
- 【骸骨】开局初始自带50诅咒；每次进入新关卡时，获得1本骸骨之书（优先放入空物品栏，已满则在脚下生成）；拾取的潘多拉魔盒会直接变为骸骨之书
- 【亡灵】每死亡1名玩家，自身-5诅咒、+20额外体力；每有一名玩家复活为人类时，自身+5诅咒；每有一名玩家复活为骷髅时，自身-10诅咒；场上每有一名骷髅玩家，自身每1分钟+15额外体力、每3分钟-2.5诅咒；每5秒使自身半径15米内的（不包括自身）人类玩家+2.5中毒、骷髅玩家+2.5额外体力并且每10秒获得一次无敌效果（奶白金）
8. **野人**
- 关键词：毒孢抗性、自然食物、周期粉蕉、禁用工具
    - “自然力量：自身对中毒与孢子有较强抗性；只能食用生的自然食物，否则获得损伤；定时获得粉莓蕉；自身无法主动使用攀爬道具，拾取会直接变为木棍”
- 【自然韧性】每5秒-2.5点中毒、-2.5孢子
- 【野性未泯】只能吃自然食物，吃包装物品（能量饮料、奶白金、大棒棒糖除外）或者烤过的食物（默认也包括自然食物）会+5损伤
- 【？不拿拿？】 每1分钟固定发放1个粉莓蕉（优先发空物品栏，已满则直接在脚下生成）
- 【知识污染】拾取岩钉、绳索炮、锁链发射器、救援抓钩、反重力炮时，这些物品直接变成木棍
9. **M**
- 关键词：负面状态转化、无负面周期惩罚、周期负面耐受
    - “负面转化：受到绝大部分负面状态时，等量转化额外体力；身上无负面状态则周期损失额外体力；对特定负面状态有耐受”
- 【享受】受到损伤、中毒、孢子、荆棘、炎热、寒冷、蛛网、箭、捕蝇草这些负面状态时，获得等量（即默认倍率为1）额外体力；每60秒检测一次自身状态，若无任何特定负面状态（损伤、中毒、孢子、荆棘、炎热、寒冷、蛛网、箭、捕蝇草），-10额外体力
- 【耐受】每10秒-5特定负面状态（损伤、中毒、孢子、荆棘、炎热、寒冷）
10. **童军**
- 关键词：开局随机物资腰包、独行流血、集体行动、同伴增益
    - “结伴而行：开局自带随机物资腰包；落单独行持续流血与饥饿；需和玩家结伴同行缓解，与同职业【童军】与【领袖】有加强羁绊”
- 【行李】开局初始默认装备一个滑稽背包，滑稽背包中会有2个随机物品（不重复）
（物品池：童军饼干、大棒棒糖、飞机餐、热狗肠、棉花糖、混合坚果、燕麦棒、黄雪莓、红脆莓、黄莓蕉、小小蛋、能量饮料、运动饮料、绳索、奶白金、绷带、急救箱、气球、一束气球、滑翔伞、自动降落伞、太阳伞、照明棒、提灯、海盗罗盘、望远镜、篮球、罗盘、飞盘、火炬、石头、护照、宾邦）
- 【集体】默认每20秒+2.5损伤、+2.5饥饿；自身半径15米内有其他玩家（无童军或领袖）时，每20秒-2.5损伤、每50秒-2.5饥饿；自身半径15米内有其他童军或领袖时，每15秒-2.5损伤、每30秒-2.5饥饿
11. **旗手**
- 关键词：检查点旗标、插旗范围清负、自身插旗石化、全员随机增益、团队爆发
    - “团队旗手：每关获得检查点旗标；自身插旗立即范围清除负面，三秒后全员获得随机增益效果；自身每次插旗增加石化”
- 【执旗】每次进入新关卡时，自动获得一个检查点旗标（优先放入空物品栏，已满则直接在脚下生成）；自身使用检查点旗标（插旗）时，会立即清除自身半径15米内所有玩家（默认包括自身）的负面状态（不包括诅咒与石化，且旗手自身会+20石化），并在3秒后使所有存活玩家（包括自身）随机获得10秒左右的无限体力效果（大棒棒糖，效果权重1，后同）或10秒左右的加速效果（能量饮料，1）或10秒左右的无敌效果（奶白金，1），这三种效果可一起叠加，即至少获得其中1种效果，默认最多获得3种效果
12. **舞王**
- 关键词：跳舞回体、强制共舞、周期后空翻、摔倒风险
    - “一起跳舞：自身范围内玩家跳舞获得额外体力；自身跳舞强制周围玩家共舞；定时强制后空翻且必定摔倒”
- 【舞动青春】自身半径15米内，每有一名玩家使用跳舞动作，自身与跳舞玩家+2.5额外体力（检测到一次后，默认会有1秒检测冷却）
- 【全场欢呼】自身进行跳舞动作时，15米范围内的玩家强制跳舞
- 【狂热舞者】自身每120秒强制后空翻1次（且后空翻后必定摔倒）
13. **待皓**
- 关键词：周期产蛋、传染转职、同类大范围回血、周期后空翻
    - “模因传播：语音与产蛋周期获得额外体力；传染式转职，可主动通过喂食煎蛋强制同化他人；较大范围内，同类周期回血减伤，但定时强制后空翻且必定摔倒”
- 【化身】本局内皮肤变成待皓同款
- 【喂喂喂，听得见吗】每60秒检测一次自身语音状态，若处于讲话状态，+10额外体力
- 【脚滑】默认每150秒强制后空翻1次（且后空翻后必定摔倒）
- 【生产】每120秒获得一个好大蛋（优先放入空物品栏，已满则在脚下生成），物品栏中存在好大蛋时，每60秒+5额外体力
- 【模因污染】本局有待皓时，其他玩家食用好大蛋 煎蛋会直接转职为待皓
- 【我们全都是】自身半径50米内除自身以外每有一个待皓，每30秒-2.5损伤
14. **圣人**
- 关键词：禁用物品、拾取消失、周期重置、受伤放大、职业免疫
    - “请输入文本：禁用食物及绝大部分道具，拾取物品直接消失；每关饥饿值清零重置；周期回满额外体力；受伤放大即倒；免疫【潘多拉之子】【死灵法师】【舞王】【区】职业效果”
- 【教诲】自身半径10米内的玩家（不包括自身），每10秒+2.5额外体力、+10困倦
- 【完美主义】每60秒+100额外体力，拾取物品100%概率直接消失（默认部分特殊物品不受影响：诡异菇、信号棒、奇怪水晶、童军的毅力、童军的进取、童军的盛情、童军的野心、童军的荣耀）
- 【辟谷养生】每次进入新关卡时，-100饥饿，并获得一次无敌效果（奶白金）
- 【跌落神坛】每1秒检测到自身受到损伤时，+100损伤
- 【超然物外】不会受潘多拉之子、死灵法师、舞王、区这些职业之效果的影响
15. **大法师**
- 关键词：周期卷轴、双形态、随机效果、负面代价
    - “随机施法：定时获得卷轴，打开随机触发效果并增加自身石化/诅咒；人类形态效果偏辅助，骷髅形态效果高收益高代价”
- 【魔法卷轴】每3分钟会获得1个卷轴（优先放入空物品栏，已满则直接脚下生成），（若自身为人类）每次打开卷轴都会立即使自身+5石化，并在3秒后随机触发下列各种效果中的1种：
    - 【强化】自身半径15米内的玩家（包括自己）获得一次加速效果（能量饮料）
    - 【激化】自身半径15米内的玩家（包括自己）获得一次无限体力效果（大棒棒糖）
    - 【固化】自身半径15米内的玩家（包括自己）获得一次无敌效果（奶白金）
    - 【点化】自身半径10米内的玩家（不包括自己）+50额外体力
    - 【净化】接下来的5秒内，自身半径10米内的玩家（包括自己）每秒-2.5负面状态
    - 【治愈】接下来的10秒内，自身半径15米内的玩家（包括自己）每秒-2.5损伤
    - 【反重】接下来的10秒内，自身半径20米内的玩家（包括自己）每5秒在脚下生成1个反重力场
    - 【传送】将自身半径10米内的玩家（包括自己）传送至自身半径50米外的随机一名玩家处
    - 【苏生】随机复活1名玩家（在身边）（童军雕像），如果没有死亡玩家可复活则自身清除所有负面状态
- 【黑魔法】若自身为骷髅，则每次打开卷轴都会立即使自身+5诅咒并立即触发卷轴效果，卷轴的各种可触发效果将会被替换为以下效果，并且每次可能随机触发不止一种效果（即触发数量下限1，上限2，有间隔1秒，再连锁触发一种效果的默认概率为30%）：
    - 【血祭】自身半径15米内的人类玩家+10损伤、+20额外体力并获得一次加速效果；每有1名玩家受到此效果影响，自身+2.5诅咒、+25额外体力；自身获得一次加速效果与无敌效果
    - 【献祭】自身半径15米内的随机1名玩家（不包括自己）被使用诅咒头骨，自身获得一次加速+无限体力+无敌效果、+5诅咒
    - 【易位】与自身半径50米外的随机一名玩家互换位置，若没有玩家符合条件，则会获得1个曲迁菇
    - 【天灾】随机1名玩家脚下生成一个龙卷风（只存在5秒），自身获得一次无敌效果并在身上绑2个一束气球
    - 【虫灾】自身半径15米内的玩家（包括自己）头顶上方生成1只甲虫，自身获得1个蜂巢
    - 【尸变】自身半径15米内的随机1名人类玩家（不包括自己）+100孢子，自身获得1个曼德拉草并+2.5诅咒；如果没有符合条件的玩家则改为在自身半径3米外生成1个僵尸
    - 【还魂】随机复活1名玩家（在身边）且该玩家变为骷髅（骸骨之书），如果没有死亡玩家可复活，则自身半径10米外生成1个童军领队（追击持续15秒）
    - 【反噬】自身半径15米内玩家（包括自己）+10诅咒、+10石化
16. **挑战者**
- 关键词：初始高负面、自带黄金抓钩、抓人高惩罚、潘多拉免疫
    - “立体机动：初始高诅咒高石化需谨慎管理；开局自带黄金抓钩与滑翔翼；使用抓钩勾中玩家会立即触发高损伤惩罚；免疫【潘多拉之子】且拾取的潘多拉餐盒直接消失”
- 【极限】开局初始默认自带60诅咒、70石化，死亡后复活会回到60诅咒、70石化
- 【顶级玩家】开局初始自带黄金抓钩、滑翔翼；不受潘多拉之子的【潘多拉】效果影响
- 【不屑】勾爪勾到玩家时，立即进入昏迷状态（+50损伤）；拾取潘多拉魔盒会直接消失
17. **特级厨师**
- 关键词：便携烤炉、烤物解饿、烤多中毒、非食物奖励、持炉炎热
    - “烤炉大师：定时获得便携烤炉；烤任意物品都能解饿；烤多会中毒；周期烤非食物（道具）可获额外体力；手持便携烤炉时有高频炎热”
- 【自带厨具】开局初始自带1个便携烤炉，每5分钟获得1个便携烤炉
- 【手暖炉】手持便携烤炉时，每1秒+2.5炎热
- 【烹饪】本局每烤制一次物品，-2.5饥饿
- 【煤气中毒】本局每烤制3次物品，+10中毒
- 【破坏大师】每1分钟内，烤制非食物物品时触发一次+10额外体力
18. **炊事员**
- 关键词：便携烤炉、生食烹饪、群体增益、熟食增益、生食惩罚、饥饿昏迷
    - “饮食保障：定时获得便携烤炉；烤熟生食可为周围回饥饿与体力；食用熟食有额外收益，食用生食有较大惩罚；附近有人饥饿会致困昏迷；与【后勤】有特殊羁绊”
- 【后勤保障】每次进入新关卡时，自动获得1个便携烤炉；每3分钟获得一个便携烤炉（优先放入空物品栏，已满则直接在脚下生成）；自身半径30米内存在后勤时，每90秒会获得一个随机食物，三个物品栏都满时不发放，且不会塞主手（参考后勤【紧急空投】）
- 【饮食供给】每次烤制（未烤过的）食物时，自身半径15米内的所有玩家（包括自己）-5饥饿、+5额外体力
- 【食品安全】每次食用烤熟的食物后额外-2.5饥饿、-2.5损伤、+10额外体力
- 【创伤应激】每次食用未烤熟的食物（诡异菇、能量饮料、奶白金、大棒棒糖除外）后+10损伤、+20中毒、+30困倦；若自身半径15米内存在饥饿值＞30的玩家（包括自身）时，每3秒+5困倦
19. **和平主义者**
- 关键词：踢击替代拉人、踢中回体、踢空困倦、初始诅咒
    - “并非和平：右键拉人变为踢人；踢中玩家获得额外体力，踢空加困倦；开局自带诅咒”
- 【和平？】开局初始自带20诅咒；右键的拉人变为踢人动作；每踢中一次玩家+2.5额外体力，若踢人未踢中玩家则-2.5额外体力、+20困倦
20. **仓鼠**
- 关键词：开局背包、物品囤积、负重奖励、周期加速、额外体力
    - “负重前行：开局自带背包；负重过半达标后，周期获得加速与额外体力”
- 【我全都要】开局初始默认装备一个背包；自身负重≥50时，每10秒获得一次加速效果、每20秒+5额外体力
21. **救火队员**
- 关键词：昏迷救援、传送无敌、救起加速、背人治疗、死亡惩罚
    - “火线救援：队友昏迷将自动传送并获得一次无敌，背起再获得一次加速与大量额外体力；背起玩家时治疗双方；有玩家死亡会获得石化惩罚”
- 【温暖臂膀】背起玩家时，每20秒自身与自身背着的玩家-2.5损伤
- 【火线救援】每当有玩家进入昏迷状态，3秒后自身（救火队员）将获得一次无敌效果，并传送至昏迷玩家处，在背起此名昏迷玩家后（自身）立即获得一次加速效果、+50额外体力；本效果默认有10秒触发冷却，并且在自身背着玩家时无法触发
- 【尽职尽责】每有人类玩家死亡，自身+15石化（该效果默认有30秒冷却）
22. **自恋狂**
- 关键词：防晒喷雾、每关刷新、强制覆盖、防晒回体、用完额外效果
    - “精致防晒：每关获得三瓶防晒喷雾，强制覆盖物品栏；获得防晒奖励额外体力，每次用完防晒喷雾会额外奖励额外体力并积累石化”
- 【化妆盒】每次进入新关卡，自动获得3个防晒喷雾（强制覆盖3个物品栏）
- 【精致】开局初始自带10诅咒；每次获得（防晒喷雾）防晒状态时，+20额外体力（该效果默认存在5秒冷却）；每使用完1瓶防晒喷雾，+30额外体力
- 【化学中毒】每使用完1瓶防晒喷雾+10石化
23. **独狼**
- 关键词：独行奖励、结伴惩罚、半径检测
    - “单人模式：离人够远时周期奖励额外体力；靠近他人持续困倦”
- 【独行长路】每30秒，自身半径50米内无其他玩家时，+10额外体力；自身半径15米内存在其他玩家时，每3秒+2.5困倦
24. **炮手**
- 关键词：童子军大炮、每关与周期获得、受伤炸药、主手风险
    - “重火力者：每关获得童子军大炮；定时获得童子军大炮；受伤就会获得炸药，且物品栏满时会直接塞入主手”
- 【开炮】每次进入新关卡时，自动获得1个童子军大炮；每3分钟获得一个童子军大炮
- 【炸膛】每次受伤时（损伤值增加时）立即获得一个炸药（优先放入空物品栏，物品栏已满会塞主手）
25. **乐队成员**
- 关键词：喇叭乐器、演奏范围增益、拾取转化、库存奖励、初始诅咒、特殊转职
    - “演奏乐队：拾取物品概率变乐器，库存与演奏可范围奖励额外体力；库存集齐三种乐器有额外加速奖励，且给予他人有低概率将其转职”
- 【乐器】拾取除食品物品以外的物品默认有50%概率不变，30%概率变为小号，10%概率变为童军领队的号角，10%概率变为友谊喇叭（除食品物品以外，默认部分特殊物品不受影响：诡异菇、小号、童军领队的号角、友谊喇叭、信号棒、奇怪水晶、童军的毅力、童军的进取、童军的盛情、童军的野心、童军的荣耀）；默认每30秒检测一次自身物品栏，若（自身物品栏中，后同）每有一个小号，+2.5额外体力；每有一个童军领队的号角，+5额外体力；每有一个友谊喇叭，+5额外体力；若同时有以上三种物品，则再额外获得一次加速效果
- 【演奏】每次吹响小号后，使自身半径15米内的所有玩家（不包括自己）+2.5额外体力（默认有1秒冷却，后同）；每次吹响童军领队的号角后，使自身+2.5额外体力；每吹响4次乐器（小号、童军领队的号角、友谊喇叭），自身+5困倦
- 【乐队】每30秒，自身半径15米内除自己外每有一名乐队成员，自身+5额外体力、-30困倦，并且【演奏】效果的影响半径+5米（最高+20米）（即默认每30秒检测一次，若符合条件，则触发效果并持续有效30秒，否则无效，直至下一次检测）；当（除乐队成员以外的）玩家物品栏中存在小号、童军领队的号角、友谊喇叭，且该玩家半径5米内存在乐队成员30秒以上时，该玩家默认有10%概率立即转职为乐队成员（默认总人数上限5）
- 【体弱】开局初始自带20诅咒
26. **领队·迈尔斯残魂**
- 关键词：童军宝石、诅咒锁定、拾取消失、特殊增益、零号守则、天底解放
    - “领队残魂：初始诅咒锁定，仅能通过获取童军宝石道具降低；使用童军宝石道具有专属特殊增益；禁用童军宝石道具以外物品，拾取物品直接消失；即时传送救援，不可放弃他人；集齐所有童军宝石道具有额外奖励；抵达天底解放灵魂全员奖励”
- 【受诅残魂】开局初始自带15诅咒；默认无法通过除自身职业效果与大法师【魔法卷轴】子效果【净化】以外的任何方式降低自身诅咒值（如被喂食烤熟的曼德拉草、被通过童军雕像复活、被喂食潘多拉餐盒等；每次进入新关卡与触发会减少自身诅咒值的职业效果后，会立即记录一次当前自身的诅咒值，当检测到自身诅咒值因自身职业效果与【净化】以外的情况而有所减少时，强制将诅咒值恢复至最新记录的诅咒值），且自身不会受到潘多拉之子的【潘多拉】效果影响
(海滩之后无法成为本职业)
- 【众魂阻挠】每次进入新关卡时+5诅咒，直至顶峰（进入顶峰与天底不会增加诅咒）；每使用任意童军宝石道具（童军的毅力、童军的进取、童军的盛情、童军的野心）13次，自身半径30米外将生成一个童军领队（追击持续30秒）
- 【封印之力】除部分特殊物品以外，无法拾取任何物品（拾取物品100%直接消失）（默认部分特殊物品不受影响：诡异菇、背包、滑稽背包、信号棒、奇怪水晶、童军的毅力、童军的进取、童军的盛情、童军的野心、童军的荣耀），拾取与使用童军宝石道具时会有特殊效果：
    - 【童军的毅力】每次（对自身）使用后立即-10石化、-10饥饿；本局首次拾取时-5诅咒
    - 【童军的进取】每次使用后立即-5石化、-5饥饿、+5额外体力；本局首次拾取时-5诅咒
    - 【童军的盛情】每次使用（且成功复制了物品）后立即-20石化、-10饥饿、+20额外体力；本局首次拾取时-5诅咒
    - 【童军的野心】每次使用后立即-25石化、-10饥饿、+25额外体力；本局首次拾取时-5诅咒
    - 【童军的力量】上述四种童军宝石道具都拾取过后，额外再-5诅咒，并清除诅咒以外的所有负面状态（包括石化）、立即获得一次无敌效果与加速效果
- 【零号守则】每30秒检测一次自身半径30米内的玩家人数（不包括自己）：每有一名玩家，+5额外体力；每次对其他昏迷的玩家使用童军的毅力时，不会使自身石化值增加（因该次使用而增加的石化值会立即等量清除，并再+30额外体力），且该次使用不计入【众魂阻挠】的次数累计；在拾取过童军的毅力后，每有一名玩家昏迷，自身将立即获得一次无敌效果并传送至昏迷玩家处，此效果默认有10秒的触发冷却；每有一名玩家死亡，自身+10损伤
- 【封印解放】进入天底，并且（有任意玩家）解放童军领队的灵魂后，立即清除在场所有玩家（包括自身）的所有负面状态（包括诅咒与石化）、+100额外体力
27. **区**
- 关键词：早起虫儿、攀爬高频脱手、分解垃圾奖励、感染转职、同类增益、免疫孢子、食虫惩罚
    - “蠕动之区：攀爬时高频强制脱手；靠拾取分解垃圾物品、获取积攒早起虫儿积累收益；感染式转职，周期对附近施加孢子，过半会使他人强制同化，并获得早起虫儿；同类聚集有加速与无限体力效果奖励；自身免疫孢子；食用早期虫儿有诅咒惩罚”
- 【！？区区？！】默认攀爬时每0.5秒强制脱手一次（在藤蔓、绳索、反重绳索、岩钉、锁链上不会触发）；死亡时，在尸体处生成一只烤熟的早起虫儿；
- 【分解者】拾取部分物品时，物品会直接消失，并使自身-5饥饿
（默认物品池：四种香蕉皮、木棍、火把、三种有毒蘑菇、弹力菇、踏板菇、云雾菇、曲迁菇、灵药菇、仙人球、魔豆、旅行指南、卷轴、撕下的书页、骸骨之书）
- 【区块链】自身每5秒-100孢子；每7秒，自身半径10米内非区玩家+2.5孢子（多名区不叠加）；自身每次喂其他玩家食用早起虫儿，会使被喂玩家+15孢子；孢子值≥50且位于区半径10米内的玩家，会直接强制转职成区，并使所有区获得一只早起虫儿
- 【好多区】每触发5次【分解者】效果，获得一只早起虫儿（优先放入空物品栏，已满则在脚下生成）；默认每60秒，检测一次自身物品栏与自身半径30米内的玩家，物品栏中每有一只早起虫儿或自身半径30米内每有一名区（同职业玩家，不包括自己），自身-2.5饥饿、+2.5额外体力（默认最高叠加4层）；若物品栏中有3只早起虫儿或自身半径20米内区的人数≥3（包括自己），则额外获得一次加速效果；若自身半径20米内区的人数≥4（包括自己），再额外获得一次无限体力效果
【区毒不食子】每食用一只早起虫儿（无论是否烤熟），+10诅咒
28. **太空人**
- 关键词：周期绑气球、拾取转化、只吃包装熟食、冷热抵抗、额外负面
    - “太空漂流：身上定时绑上气球；无法拾取气球/一束气球；拾取物品概率变为火箭背包/喷气背包；只吃包装食品或熟食，否则中毒；对冷热有一定抗性，但部分负面状态会额外加重”
- 【轻飘飘】开局初始自身会绑上2个气球，之后每过2分钟会再次绑上2个气球；拾取气球或一束气球会直接消失
- 【飞起来】拾取非食物物品与部分特殊物品以外的物品，有20%概率变为火箭背包，10%概率变成喷气背包（概率判定成功时，拾取物品消失，并在脚下生成一个火箭背包或喷气背包）（默认部分特殊物品不受影响：诡异菇、火箭背包、喷气背包、信号棒、奇怪水晶、童军的毅力、童军的进取、童军的盛情、童军的野心、童军的荣耀）
- 【太空食品安全】只能食用包装食物与烤熟的食物，每次食用不符合条件的食物会+10中毒（不包括诡异菇、万灵药）
- 【我去，老美的NASA这么厉害】每5秒，-5炎热、-5寒冷；每次获得（有增加）蛛网、捕蝇草、诅咒时，额外+2.5相应值
29. **榔王**
- 关键词：周期荔莓、食用奖励、非莓惩罚、进食损伤、特殊转职
    - “大果嚼嚼：定时获得青/紫荔莓；食用青荔莓获得冲刺效果、紫荔莓则给无限体力；食用其他食物加困倦；每次进食固定损伤；他人多次连吃荔莓会被强制同化”
- 【精品大果】（整两口！）每100秒，获得一个青荔莓（默认物品权重为10，后同）或紫荔莓（5）（优先放入空物品栏，已满则直接在脚下生成）；自身每次食用青荔莓后，获得一次加速效果；食用紫荔莓后，获得一次无限体力效果
- 【上瘾】（这个没劲啊）每次食用除各种荔莓以外的食物后，+15困倦
- 【纤维化】（默哀）每次食用物品后，+2.5损伤（诡异菇、万灵药、能量饮料、奶白金、大棒棒糖除外）
- 【没吃的赶紧戒】（你也整两口）本局有榔王时，其他玩家若连续三次以上食用任意荔莓（连续次数＞3），该玩家将强制转职为榔王
30. **程序员**
- 关键词：周期获得昆虫、额外体力溢出取模、夜间加速、独行清负、职业效果复制
    - “代码之力：定时获得随机虫类，获得时大概率中毒；额外体力溢出会取模重置；夜晚周期获得加速；独行会周期清负面；无负面且无虫时会复制在场他人职业效果，并持续生效一段时间”
- 【BUG】每180秒获得一个随机昆虫（蜱虫70%、蝎子20%、早起虫儿10%；优先放入空物品栏，已满则不发放），若为蜱虫，则立刻+10中毒；若为蝎子，则立刻+25中毒
- 【溢出】额外体力越多，额外体力越少：每当自身额外体力增加时，如果 (已有额外体力+本次额外体力增加量)>100，那么额外体力将变为 (已有额外体力+增加量) mod 100（例如当你额外体力为95时，吃下一个本来会增加10额外体力的食物，那么你的额外体力会变成5）
- 【熬夜】每60秒检测一次，如果时间为晚上，获得一次能量饮料效果，且结束后不额外消除因此产生的困倦
- 【开挂】每10秒检测一次，如果自身半径30米内没有其他玩家，就使自身任意一个随机负面状态-2.5（包括损伤、孢子、中毒、睡眠、寒冷、炎热）；每3分钟检测一次，若自身没有任何负面状态（损伤、孢子、中毒、睡眠、寒冷、炎热）且物品栏中没有任意昆虫BUG（蜱虫、蝎子、早起虫儿），则获得在场随机一名玩家当前职业的随机一个非负面职业效果，持续有效3分钟直至下一次检测