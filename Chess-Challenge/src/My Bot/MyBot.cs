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

    // PST Piece Square Table: Pawn, Knight, Bishop, Rook, Queen, King
    static readonly short[] PieceSquareTable = new short[]
    {
        // Pawn
        0,   0,   0,   0,   0,   0,   0,   0,
        78,  83,  86,  73, 102,  82,  85,  90,
        7,  29,  21,  44,  40,  31,  44,   7,
        -17,  16,  -2,  15,  14,   0,  15, -13,
        -26,   3,  10,   9,   6,   1,   0, -23,
        -22,   9,   5, -11, -10,  -2,   3, -19,
        -31,   8,  -7, -37, -36, -14,   3, -31,
        0,   0,   0,   0,   0,   0,   0,   0,
        // Knight
        -66, -53, -75, -75, -10, -55, -58, -70,
        -3,  -6, 100, -36,   4,  62,  -4, -14,
        10,  67,   1,  74,  73,  27,  62,  -2,
        24,  24,  45,  37,  33,  41,  25,  17,
        -1,   5,  31,  21,  22,  35,   2,   0,
        -18,  10,  13,  22,  18,  15,  11, -14,
        -23, -15,   2,   0,   2,   0, -23, -20,
        -74, -23, -26, -24, -19, -35, -22, -69,
        // Bishop
        -59, -78, -82, -76, -23,-107, -37, -50,
        -11,  20,  35, -42, -39,  31,   2, -22,
        -9,  39, -32,  41,  52, -10,  28, -14,
        25,  17,  20,  34,  26,  25,  15,  10,
        13,  10,  17,  23,  17,  16,   0,   7,
        14,  25,  24,  15,   8,  25,  20,  15,
        19,  20,  11,   6,   7,   6,  20,  16,
        -7,   2, -15, -12, -14, -15, -10, -10,
        // Rook
        35,  29,  33,   4,  37,  33,  56,  50,
        55,  29,  56,  67,  55,  62,  34,  60,
        19,  35,  28,  33,  45,  27,  25,  15,
        0,   5,  16,  13,  18,  -4,  -9,  -6,
        -28, -35, -16, -21, -13, -29, -46, -30,
        -42, -28, -42, -25, -25, -35, -26, -46,
        -53, -38, -31, -26, -29, -43, -44, -53,
        -30, -24, -18,   5,  -2, -18, -31, -32,
        // Queen
        6,   1,  -8,-104,  69,  24,  88,  26,
        14,  32,  60, -10,  20,  76,  57,  24,
        -2,  43,  32,  60,  72,  63,  43,   2,
        1, -16,  22,  17,  25,  20, -13,  -6,
        -14, -15,  -2,  -5,  -1, -10, -20, -22,
        -30,  -6, -13, -11, -16, -11, -16, -27,
        -36, -18,   0, -19, -15, -15, -21, -38,
        -39, -30, -31, -13, -31, -36, -34, -42,
        // King
        4,  54,  47, -99, -99,  60,  83, -62,
        -32,  10,  55,  56,  56,  55,  10,   3,
        -62,  12, -57,  44, -67,  28,  37, -31,
        -55,  50,  11,  -4, -19,  13,   0, -49,
        -55, -43, -52, -28, -51, -47,  -8, -50,
        -47, -42, -43, -79, -64, -32, -29, -32,
        -4,   3, -14, -50, -57, -18,  13,   4,
        17,  30,  -3, -14,   6,  -1,  40,  18,
    };

    short GetPieceSquareTableValue(PieceType pieceType, int rank, int file, bool whiteToPlay = true)
    {
        // Flip the rank if whiteToPlay is true
        // Piece Square Table: Pawn, Knight, Bishop, Rook, Queen, King vs
        // pieceType which is None, Pawn, Knight, Bishop, Rook, Queen, King so -1 to remove None.
        return PieceSquareTable[(int)(pieceType-1) * 64 + (whiteToPlay ? 7 - rank : rank) * 8 + file];
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
                score += pieceValues[i+1] + GetPieceSquareTableValue(piece.PieceType, piece.Square.Rank, piece.Square.File); 
            }
            // Black
            foreach (Piece piece in piecelists[i+6])
            {
                score -= pieceValues[i+1] + GetPieceSquareTableValue(piece.PieceType, piece.Square.Rank, piece.Square.File, false);
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
            
            // capture D8D5
            // if (maxPly >= 2 && move.StartSquare.File == 3 && move.StartSquare.Rank == 7
            //                 && move.TargetSquare.File == 3 && move.TargetSquare.Rank == 4) {
            //     Console.WriteLine($" SPECIAL {move} - score {score} response {next_move} {Evaluate(board)}");
            // }
        }
        // if (maxPly >= 2) {
        //     Console.WriteLine($"End Search({maxPly} ply - {side}) - Found (score {alpha}) {moveToPlay} expecting {expected_response}");
        // }
        return (alpha, moveToPlay, expected_response);
    }

    public Move Think(Board board, Timer timer)
    {
        moveCount = 0;
        pruneCount = 0;
        exchangeCount = 0;
        exchangePruneCount = 0;
        int maxPly = 4;
        // // Drop search depth as less time remains
        // if (timer.MillisecondsRemaining < 1) {
        //     maxPly = 1;
        //     exchangeDepth = 0;
        // } else if (timer.MillisecondsRemaining < 10) {
        //     maxPly = 2;
        //     exchangeDepth = 1;
        // } else if (timer.MillisecondsRemaining < 30) {
        //     maxPly = 3;
        //     exchangeDepth = 2;
        // }
        
        this.board = board;
        Console.WriteLine($"---- NEW THINK {board.GetFenString()}");
        int curScore = Evaluate(negInf, posInf);
        // Console.WriteLine($"Score before any moves {curScore}");
        (int bestScore, Move moveToPlay, Move expected_response) = Search(maxPly, negInf, posInf);
        // Console.WriteLine(board.GetFenString());
        Console.WriteLine($"Searched {moveCount} moves / {exchangeCount} exchanges in {timer.MillisecondsElapsedThisTurn} ms- Chose {moveToPlay} score {curScore} -> {bestScore}, expecting {expected_response} - (pruned {pruneCount}, exchanges pruned {exchangePruneCount})");

        return moveToPlay;
        
        // doTest(timer);
        // return Move.NullMove;
    }

    // public void doTest(Timer timer) {
    //     moveCount = 0;
    //     int maxPly = 3;
    //     // String test1 = "1rbqkbnr/pppppppp/8/3P4/1n2P3/2P5/PP3PPP/RNBQKBNR b KQk - 0 4";
    //     // String test1 = "r2qkbnr/pppbpppp/4p3/1B1P4/Q1P2B2/2Nn1N2/P2K1PPP/R6R b k - 3 13";
    //     // String test1 = "2Q2b1r/p3pppp/4k3/3R4/5B2/2N2N2/P4KPP/8 b - - 2 24"; // king walk into checkmat
    //     // String test1 = "rnbqkb1r/pppp1ppp/4p2n/8/3PP3/5P2/PPP3PP/RNBQKBNR b KQkq - 0 3"; // queen shouldn't go out
    //     String test1 = "r3kb1r/ppp1pppp/2n2n2/8/1q4b1/2N1BN1P/PPPPQPP1/2KR1B1R b Kkq - 0 1"; // ply3 bishop g4 should take knight f3 or move, but did pawn e7e5
    //     Board board = Board.CreateBoardFromFEN(test1);
    //     Console.WriteLine($"Testing {test1} : {board.IsWhiteToMove}");
    //     (int bestScore, Move moveToPlay, Move expected_response) = Search(board, maxPly);
    //     Console.WriteLine($"{board.GetFenString()} | Starting score {Evaluate(board, false)} vs captures {Evaluate(board)}");
    //     String side = board.IsWhiteToMove ? "WHITE" : "BLACK";
    //     Console.WriteLine($"Searched {moveCount} moves in {timer.MillisecondsElapsedThisTurn} ms - {side} - Chose {moveToPlay} score {bestScore}, expecting {expected_response}");
    // }
}
