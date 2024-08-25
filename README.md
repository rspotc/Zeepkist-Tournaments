The Top->Out Tournament is a competition where players gain championship points until reaching a pre-defined point threshold. Afterwards the player needs to win a round to secure victory in the tournament.

The mod uses the in-game championship point awards for points accumulation. However, unlike the base game, these points are remembered and retained even if the player leaves the lobby and returns while the tournament is still active (i.e. there are no penalties for disconnecting from the lobby). Points are only awarded to the top 16 players each round as follows:

<ol><li>64</li>
<li>48</li>
<li>36</li>
<li>27</li>
<li>20</li>
<li>15</li>
<li>11</li>
<li>8</li>
<li>6</li>
<li>5</li>
<li>4</li>
<li>3</li>
<li>2</li>
<li>1</li>
<li>1</li>
<li>1</li></ol>

Many settings are available to mix-up the play style of the tournament:

<ul><li>Number of winners 1-10. The tournament will keep going until it is manually stopped or the number of winners has been achieved. Once someone become a winner they will be put into photomode if they have the mod, and kicked if they put down a time.</li>
    <li>Points threshold 64-6400 (absolute threshold). The number of points required to become eligible to win the tournament is configurable. Additionally, the tournament can be specified by a minimum number of rounds by turning off the "Use Absolute Threshold" setting. The points threshold value will then be calculated as (64 * (rounds - 2) + 1). This ensures that if a single player wins every round at least the configured amount of round will be played.</li>
    <li>Round timer 30-86400 seconds. The base length of each round is configurable.</li>
    <li>Countdown timer 30-86400 seconds. If the setting "Set Countdown on First Finish" is selected, the timer will reset to the value specified with the "Countdown Time from First Finish" setting as soon as the first player crosses the finish line. All players then have this amount of time left to finish.</li>
    <li>Room Visibility. The "Private Lobby for Tournament" setting makes the lobby private when the tournament starts and public again when it ends.</li></ul>

Tournaments are initiated with the chat command

/topout start

This will send a message in chat that triggers the tournament for all other players with the mod as well. Only the host of the lobby may start a tournament. Anyone can use

/topout stop

to end their local tournament. If host a chat command will be sent to trigger the end of the tournament for other mod users. The command

/topout join

can be used by non-host players to join an already active tournament. Simply being in the lobby will make a player a participant in the tournament, but joining will make the UI available to them as well.

Because the tournament is highly configurable, the tournament parameters are not known by non-host players immediately. In order to get the parameters the mod for non-host players must be active for a full round of the tournament. If the host changes hands for any reason the tournament tracking will end for those that do not have the parameters determined, at which point it must be re-joined. This means that if the host is given to a player that doesn't have the parameters locked in, the tournament will end for everyone.

A player who joins a tournament after start may also not know how many points are really accumulated by players, or whether they have won a round after reaching the point threshold. Then this player's copy of the mod must use messages sent by the host throughout the tournament to try to determine standings of players. As such, it may not have completely accurate information.
