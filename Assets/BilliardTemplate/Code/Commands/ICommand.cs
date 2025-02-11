/*
 *  Created by Dragutin Sredojevic.
 *  https://www.nitugard.com
 */

namespace ibc.commands
{

    /// <summary>Interface used for commands(command pattern).</summary>
    public interface IBilliardCommand
    {

        /// <summary>
        /// Executes command.
        /// </summary>
        /// <param name="state">The billiard state on which command can act upon.</param>
        bool Execute(BilliardState state);
    }
}
