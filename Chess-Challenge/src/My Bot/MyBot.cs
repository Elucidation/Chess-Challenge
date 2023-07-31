using System;
using System.Collections.Generic;
using ChessChallenge.API;

public class MyBot : IChessBot
{
    // Piece values: none, pawn, knight, bishop, rook, queen, king, maps to PieceType enum as int
    static readonly int[] pieceValues = { 0, 100, 280, 320, 479, 929, 60000 };
    Board board;
    int exchangeDepth = 1_000;
    const int posInf = 1_000_000_000;
    const int negInf = -posInf;
    // Choose random move as start move
    Random rng = new(1234);

    short GetPieceSquareTableValue(int rank, int file, bool whiteToPlay = true)
    {
        // Flip the rank if whiteToPlay is true
        int rankFactor = (whiteToPlay ? 7 - rank : rank); // Prefer to go slightly higher rank
        int fileFactor = -Math.Abs(file - 4) + 4; // Prefer to be in the center
        return (short)(rankFactor*10 + fileFactor*20);
    }


    int moveCount = 0; // Track how many moves were made when searching
    int pruneCount = 0;
    int exchangeCount = 0; // Track how many exchanges were made when searching
    int exchangePruneCount = 0;

    // Return a int score for given board position, for the piece to play currently
    public int Evaluate(int alpha, int beta)
    {
        // Console.WriteLine($"Evaluate({alpha} A, {beta} B) - {board.IsWhiteToMove}");
        if (board.IsInCheckmate()) return negInf; // Lose
        else if (board.IsDraw()) return 0; // Draw
        return ExchangeCalculator(exchangeDepth, alpha, beta);
    }
    public int GetBoardScore() {
        // Returns score of board, + favors current side to play
        // Calculate score, positive means better for side whose turn it is.
        // Initially calculated where negative means better for black, positive for white.
        if (board.IsInCheckmate()) return negInf; // Lose for side
        else if (board.IsDraw()) return 0; // Draw
        int score = 0;
        PieceList[] piecelists = board.GetAllPieceLists();
        // White pieces +, Black pieces -
        for (int i = 0; i < 6; i++)
        {
            // White
            foreach (Piece piece in piecelists[i])
            {
                score += pieceValues[i+1] + GetPieceSquareTableValue(piece.Square.Rank, piece.Square.File); 
            }
            // Black
            foreach (Piece piece in piecelists[i+6])
            {
                score -= pieceValues[i+1] + GetPieceSquareTableValue(piece.Square.Rank, piece.Square.File, false);
            }
        }
        return board.IsWhiteToMove? score : -score; // Positive for side if preferred
    }

    public int ExchangeCalculator(int depth, int alpha, int beta) {
        // Returns score for swapping down capturable material or not capturing if it's worse.
        // positive favors side to play on board.
        int score = GetBoardScore();
        // Console.WriteLine($"Exchange(depth={depth}) - init score {score} - {board.GetFenString()}");
        String side = board.IsWhiteToMove ? "WHITE" : "BLACK";
        // Console.WriteLine($"ExchangeCalculator({depth} depth, {alpha} A, {beta} B) - {side} - Have {board.GetLegalMoves(true).Length} captures available");
        if (depth == 0 || score >= beta) {
            // Console.WriteLine($"score {score} >= beta {beta}, dropping out early.");
            return score;
        }
        alpha = Math.Max(alpha, score);
        

        // Search through only capturing moves
        foreach (Move capture_move in board.GetLegalMoves(true))
        {
            var piece_capturing = board.GetPiece(capture_move.StartSquare);
            var piece_captured = board.GetPiece(capture_move.TargetSquare);
            // Skip moves that are worse or equal trades, this is inaccurate but saves a lot of time
            if ((pieceValues[(int)piece_captured.PieceType] <= pieceValues[(int)piece_capturing.PieceType])) continue;

            // Console.WriteLine($"Exchange(depth={depth}) {side} - Potential capture {capture_move} - A {alpha} B {beta}");
            // Appear to have something to gain, see if the score improves with capture or not.
            board.MakeMove(capture_move);
            // Negate for opponent, alpha-beta pruning
            int new_score = -ExchangeCalculator(depth-1, -beta, -alpha);
            board.UndoMove(capture_move);
            exchangeCount++;
            // Console.WriteLine($"  Score {new_score}");
            if (new_score >= beta) {
                // Move too good, opponent avoids.
                exchangePruneCount++;
                // Console.WriteLine($"Exchange(depth={depth}) {side} - Pruning at {capture_move} {new_score} >= {beta}");
                return beta;
            }
            if (new_score > alpha) {
                // Console.WriteLine($"Exchange(depth={depth}) {side} - doing capture {capture_move} - alpha {alpha} -> {new_score}");
                //  {piece_capturing} {pieceValues[(int)piece_capturing.PieceType]} -> {piece_captured} {pieceValues[(int)piece_captured.PieceType]}");
                alpha = new_score;
            }
            
            // alpha = Math.Max(alpha, new_score); // New best capture
        }
        return alpha;
    }

    // Use negamax search
    public (int, Move, Move) Search(int maxPly, int alpha, int beta)
    {
        if (board.IsInCheckmate() || board.IsDraw() || maxPly == 0)
        {
            return (Evaluate(alpha, beta), Move.NullMove, Move.NullMove);
        }
        // Evaluate score of board assuming optimal choices up to a maxPly.
        Move[] allMoves = board.GetLegalMoves();
        Move moveToPlay = allMoves[0]; // If all moves are losing, just play first one.
        Move expected_response = Move.NullMove;
        String side = board.IsWhiteToMove ? "WHITE" : "BLACK";
        // if (maxPly >= 2) {
        //     String allMovesStr = String.Join(", ", allMoves);
        //     Console.WriteLine($"Search({maxPly} ply, {alpha} A, {beta} B) - {side} - Have {allMoves.Length} moves: {allMovesStr}");
        // }

        foreach (Move move in allMoves)
        {
            // Make move and recursively get negative negamax score (for opponent) to maxPly
            board.MakeMove(move);
            // if (maxPly >= 0) Console.WriteLine($" ({maxPly} ply - {side}) - {move} ({alpha} A, {beta} B) - Recursive Search down");
            (int score, Move next_move, _) = Search(maxPly - 1, -beta, -alpha);
            score = -score; // invert since opponents best score is our worst.
            board.UndoMove(move);
            moveCount++;
            // if (maxPly >= 2) Console.WriteLine($" ({maxPly} ply - {side}) - {move} - End Recursive Search down, score {score}");

            if (score >= beta) {
                // Move too good, opponent avoids.
                // if (maxPly >= 0 && board.SquareIsAttackedByOpponent(move.StartSquare)) Console.WriteLine($" ({maxPly} ply - {side}) - {move} -  Prune curr {alpha} {beta} vs score {score}");
                pruneCount++;
                return (beta, move, Move.NullMove);
            }
            if (score > alpha) // Note: because of pruning can't choose alternatives score==alpha here as it may be a bad prune?
            { 
                // New best move
                int prevAlpha = alpha;
                alpha = score;
                moveToPlay = move;
                expected_response = next_move;
                // if (maxPly >= 0 && board.SquareIsAttackedByOpponent(move.StartSquare)) Console.WriteLine($" ({maxPly} ply - {side}) - {move} -  prev {prevAlpha} new alpha {alpha} response {next_move}");
            }
        }
        return (alpha, moveToPlay, expected_response);
    }
    // public int getPieceCount() {
    //     return System.Numerics.BitOperations.PopCount(board.AllPiecesBitboard);
    // }

    public Move Think(Board board, Timer timer)
    {
        this.board = board;
        moveCount = 0;
        pruneCount = 0;
        exchangeCount = 0;
        exchangePruneCount = 0;
        int maxPly = 4;
        // TODO: Move Ordering for better pruning, aim for higher ply.
        // Drop search depth as less time remains
        if (timer.MillisecondsRemaining < 1) {
            maxPly = 1;
            exchangeDepth = 0;
        } else if (timer.MillisecondsRemaining < 10) {
            maxPly = 2;
            exchangeDepth = 1;
        } else if (timer.MillisecondsRemaining < 30) {
            maxPly = 3;
            exchangeDepth = 2;
        }
        // // If less than 10 pieces left, go as deep as we want
        // if (getPieceCount() < 10) {
        //     maxPly = 8;
        //     exchangeDepth=1_000;
        // }
        
        
        Console.WriteLine($"---- NEW THINK PLY {maxPly} exchangeDepth {exchangeDepth} - {board.GetFenString()} ");
        int curScore = Evaluate(negInf, posInf);
        // Console.WriteLine($"Score before any moves {curScore}");
        (int bestScore, Move moveToPlay, Move expected_response) = Search(maxPly, negInf, posInf);
        // Console.WriteLine(board.GetFenString());
        Console.WriteLine($"Searched {moveCount} moves / {exchangeCount} exchanges in {timer.MillisecondsElapsedThisTurn} ms- Chose {moveToPlay} score {curScore} -> {bestScore}, expecting {expected_response} - (pruned {pruneCount}, exchanges pruned {exchangePruneCount})");

        return moveToPlay;
    }
}
