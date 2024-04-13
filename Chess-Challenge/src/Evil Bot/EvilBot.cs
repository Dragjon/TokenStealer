using ChessChallenge.API;
using System.Collections.Generic;
using static System.Math;
using System.Linq;
namespace ChessChallenge.Example
{
    // A simple bot that can spot mate in one, and always captures the most valuable piece it can.
    // Plays randomly otherwise.
    public class EvilBot : IChessBot
    {
        // Just middle game values here
        public static readonly int[] PieceValues = { 85, 303, 311, 417, 884, 0 },
        UnpackedPestoTables =
            new[] {
            14582380538636468983303241728m, 76147952343528119540275822370m, 1853278318432638012796110329m, 77390600027349219094131180296m, 17659471727722626798m, 62506069751439917757035970560m, 9925422225107838129372654299m, 3415376334789177808020324394m,
            1554654788992408289091781113m, 76124892436466096543867078665m, 73298390554665608892934976715m, 71811539854197568317216189171m, 7748015004129416562957685240m, 625043297472753915073335826m, 9704500378620837147117312m, 75799620444163281462657812995m,
            9629169952419356380140410128m, 2512251894096480897380196648m, 78911362244444695094490233332m, 73963290606085331150621047812m, 2475846853389771486073845738m, 6834162085735986152233304840m, 1241524188268196264608066548m, 79226991366797857916592790542m,
            78911428561067487264952218363m, 79225777793050925211148812541m, 76762005196180037506032137983m, 74562908276007239482733555428m, 74888071449819503520292867067m, 71160896220639830623598998257m, 69319820681237954860697319673m, 2180836155743904792437848042m
            }
                // decimal.GetBits returns i32[4]
                // last int is useless so only take 3
                .SelectMany(i => decimal.GetBits(i).Take(3))
                .SelectMany(System.BitConverter.GetBytes)
                // Have to cast to sbyte to have the right sign
                .Select((i, index) => (sbyte)i + PieceValues[index / 64])
                .ToArray();


        public Move Think(Board board, Timer timer)
        {
            Node rootNode = new(Move.NullMove),
                node;

            // Search for 1/30 of remaining time
            int searchTime = timer.MillisecondsRemaining / 30, sqIndex;
            while (timer.MillisecondsElapsedThisTurn < searchTime)
            {
                Stack<Node> path = new(new[] { node = rootNode });

                // Loop until we find a node that isn't expanded yet
                while (node.children.Count > 0)
                {
                    // Select best child
                    node = node.children.MaxBy(child => child.score / child.visits + Sqrt(2 * Log(node.visits) / child.visits));

                    // Do the stuff with the child
                    // instructions unclear: in jail
                    board.MakeMove(node.move);
                    path.Push(node);
                }

                // Expand the node
                board.GetLegalMoves().ToList().ForEach(move => node.children.Add(new(move)));

                // Evaluate node
                ulong pieces = board.AllPiecesBitboard;
                double score = 0;

                while (pieces > 0)
                {
                    Piece piece = board.GetPiece(new(sqIndex = BitboardHelper.ClearAndGetIndexOfLSB(ref pieces)));

                    score += UnpackedPestoTables[sqIndex ^ (piece.IsWhite ? 56 : 0) + (int)piece.PieceType * 64 - 64]
                     // Divide by 100 since otherwise even slight
                     // advantages get close to 1 after Tanh
                     * (piece.IsWhite == board.IsWhiteToMove ? 0.01 : -0.01);
                }

                // 'normalize' score
                score =
                    // Terminal nodes
                    board.IsInCheckmate() ? -1 :
                    board.IsDraw() ? 0 :
                    // Normal nodes
                    Tanh(score)
                    // Scale eval so that 0.8 is the highest possible score
                    // This way the mate score of 1.0 looks way better to UCT
                    * 0.8;

                // Backprop results
                while (path.TryPop(out node))
                {
                    board.UndoMove(node.move);
                    node.visits++;
                    node.score += score *= -1;
                }
            }

            return rootNode.children.MaxBy(node => node.score / node.visits).move;
        }
    }

    // Can't have it as a tuple because it needs to be mutable
    // Maybe something could be done with "ref" but I haven't tried
    public record class Node(Move move)
    {
        // Maybe could be shorter with arraylist
        // but linq doesn't let me do that
        public List<Node> children = new();

        // Can't start visits at zero because of division
        public double visits = 1,

        // default value of 0
        score;
    }
}