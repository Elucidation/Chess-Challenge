using System;
using System.Collections.Generic;
using ChessChallenge.API;

public class MyBot : IChessBot
{
    // Piece values: none, pawn, knight, bishop, rook, queen, king, maps to PieceType enum as int
    int[] pieceValues = { 0, 100, 280, 320, 479, 929, 60000 };

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

    // Return a int score for given board position, for the piece to play currently
    public int Evaluate(Board board, bool doCaptures = true)
    {
        if (board.IsInCheckmate()) { return int.MinValue; } // Lose
        else if (board.IsDraw()) { return 0; } // Draw
        // Else, count up the pieces by their values and return the sum.
        PieceList[] piecelists = board.GetAllPieceLists();

        List<Move> tracked_moves = new List<Move>(); // 
        bool didCapture = true;
        Move[] capture_moves;
        // Swap Material of > value or undefended until no material is left.
        // Keep doing this till no more valid captures exist.
        // Only keep going if a valid capture happened
        // Note, this follows the chain of the first valid capture, it doesn't try the others,
        // so this is not optimal, but it is simpler and okay for a first pass heuristic.)
        while (doCaptures && didCapture)
        {
            capture_moves = board.GetLegalMoves(true);
            didCapture = false;
            int best_capture_value = 0;
            Move best_capture_move = Move.NullMove;
            foreach (Move capture_move in capture_moves)
            {
                if (capture_move.IsNull) continue;
                var piece_capturing = board.GetPiece(capture_move.StartSquare);
                var piece_captured = board.GetPiece(capture_move.TargetSquare);
                int capture_value;
                if (!board.SquareIsAttackedByOpponent(capture_move.TargetSquare)) {
                    // Free piece, full value of captured piece (or pawn if en-passant)
                    capture_value = capture_move.IsEnPassant ? pieceValues[(int)PieceType.Pawn] : pieceValues[(int)piece_captured.PieceType];
                } 
                else 
                {
                    // Better trade, difference in piece values
                    capture_value = pieceValues[(int)piece_captured.PieceType] - pieceValues[(int)piece_capturing.PieceType];
                }
                // Console.WriteLine($"{piece_capturing} {pieceValues[(int)piece_capturing.PieceType]} -> {piece_captured} {pieceValues[(int)piece_captured.PieceType]}");
                // Do the first capture that improves the position.
                if (capture_value > best_capture_value)
                {
                    best_capture_value = capture_value; // Replace with new best capture
                    best_capture_move = capture_move;
                    // String current_side = board.IsWhiteToMove ? "WHITE" : "BLACK";
                    // Console.WriteLine($"{tracked_moves.Count} {current_side} - Making capture {capture_move} {piece_capturing} takes {piece_captured} {board.GetFenString()}");
                    // tracked_moves.Add(capture_move);
                    // board.MakeMove(capture_move);
                    didCapture = true;
                    // break;
                }
            }
            if (didCapture) {
                // String current_side = board.IsWhiteToMove ? "WHITE" : "BLACK";
                // var piece_capturing = board.GetPiece(best_capture_move.StartSquare);
                // var piece_captured = board.GetPiece(best_capture_move.TargetSquare);
                // Console.WriteLine($"{tracked_moves.Count} {current_side} - Making capture {best_capture_move} {piece_capturing} takes {piece_captured} {board.GetFenString()}");
                tracked_moves.Add(best_capture_move);
                board.MakeMove(best_capture_move);
            }
        }


        // Calculate score, positive means better for side whose turn it is.
        // Initially calculated where negative means better for black, positive for white.
        int score = 0;
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

        // Revert all the capture moves (Note: only check side to move after reverting)
        tracked_moves.Reverse();
        foreach (Move capture_move in tracked_moves)
        {
            board.UndoMove(capture_move);
        }
        if (board.IsInCheckmate()) { return int.MinValue; } // Lose
        else if (board.IsDraw()) { return 0; } // Draw
        return board.IsWhiteToMove ? score : -score; // Positive score for side playing
    }

    // Use negamax search
    public (int, Move, Move) Search(Board board, int maxPly = 1)
    {
        if (maxPly == 0)
        {
            return (Evaluate(board), Move.NullMove, Move.NullMove);
        }
        // Evaluate score of board assuming optimal choices up to a maxPly.
        Move[] allMoves = board.GetLegalMoves();
        if (allMoves.Length == 0)
        {
            // No legal moves here, means we're in check or stalemate.
            return (Evaluate(board), Move.NullMove, Move.NullMove);
        }
        Move moveToPlay = Move.NullMove;
        Move expected_response = Move.NullMove;
        int bestScore = int.MinValue;

        // Choose random move as start move
        Random rng = new();
        
        String side = board.IsWhiteToMove ? "WHITE" : "BLACK";
        // if (maxPly >= 2) {
        //     String allMovesStr = String.Join(", ", allMoves);
        //     Console.WriteLine($"Search({maxPly} ply - {side} - Have {allMoves.Length} moves: {allMovesStr})");
        // }

        foreach (Move move in allMoves)
        {
            // Make move and recursively get negative negamax score (for opponent) to maxPly
            board.MakeMove(move);
            if (board.IsInCheckmate())
            {
                board.UndoMove(move);
                // This put opponent in checkmate, consider this the best move and break out.
                moveToPlay = move;
                expected_response = Move.NullMove;
                bestScore = int.MaxValue;
                break;
            }
            // if (maxPly >= 2) Console.WriteLine($" ({maxPly} ply - {side}) - {move} - Recursive Search down");
            (int score, Move next_move, Move next_move_response) = Search(board, maxPly - 1);
            board.UndoMove(move);
            score = -score; // invert for negamax, opponents best score is our worst.
            moveCount += 1;
            // if (maxPly >= 2) Console.WriteLine($" ({maxPly} ply - {side}) - {move} - End Recursive Search down, score {score}");

            // + score is better for current player
            // If the scores are equal, just randomly choose the next option sometimes.
            // This is pretty arbitrary, but mixes things up a bit
            if (score > bestScore || (score == bestScore))
            {
                moveToPlay = move;
                expected_response = next_move;
                int prevBestScore = bestScore;
                bestScore = score;
                // if (maxPly >= 2 && move.IsCapture) Console.WriteLine($" ({maxPly} ply - {side}) - {move} -  prev {prevBestScore} new best {bestScore} response {next_move}");
            }
            // capture D8D5
            // if (maxPly >= 2 && move.StartSquare.File == 3 && move.StartSquare.Rank == 7
            //                 && move.TargetSquare.File == 3 && move.TargetSquare.Rank == 4) {
            //     Console.WriteLine($" SPECIAL {move} - score {score} response {next_move} {Evaluate(board)}");
            // }
        }
        // if (maxPly >= 2) {
        //     Console.WriteLine($" Found (score {bestScore}) {moveToPlay} expecting {expected_response}");
        //     Console.WriteLine($"End Search({maxPly} ply - {side})");
        // }
        return (bestScore, moveToPlay, expected_response);
    }

    public Move Think(Board board, Timer timer)
    {
        moveCount = 0;
        int maxPly = 2;
        // Console.WriteLine($"\n---- NEW THINK {board.GetFenString()} \n");
        int curScore = Evaluate(board);
        (int bestScore, Move moveToPlay, Move expected_response) = Search(board, maxPly);
        // Console.WriteLine(board.GetFenString());
        Console.WriteLine($"Searched {moveCount} moves in {timer.MillisecondsElapsedThisTurn} ms- Chose {moveToPlay} score {curScore} -> {bestScore}, expecting {expected_response}");

        return moveToPlay;
        
        // doTest();
        // return Move.NullMove;
        
    }

    // public void doTest() {
    //     moveCount = 0;
    //     int maxPly = 3;
    //     // String test1 = "1rbqkbnr/pppppppp/8/3P4/1n2P3/2P5/PP3PPP/RNBQKBNR b KQk - 0 4";
    //     // String test1 = "r2qkbnr/pppbpppp/4p3/1B1P4/Q1P2B2/2Nn1N2/P2K1PPP/R6R b k - 3 13";
    //     // String test1 = "2Q2b1r/p3pppp/4k3/3R4/5B2/2N2N2/P4KPP/8 b - - 2 24"; // king walk into checkmat
    //     String test1 = "rnbqkb1r/pppp1ppp/4p2n/8/3PP3/5P2/PPP3PP/RNBQKBNR b KQkq - 0 3"; // queen shouldn't go out
    //     Board board = Board.CreateBoardFromFEN(test1);
    //     Console.WriteLine($"Testing {test1} : {board.IsWhiteToMove}");
    //     (int bestScore, Move moveToPlay, Move expected_response) = Search(board, maxPly);
    //     Console.WriteLine($"{board.GetFenString()} | Starting score {Evaluate(board, false)}");
    //     String side = board.IsWhiteToMove ? "WHITE" : "BLACK";
    //     Console.WriteLine($"Searched {moveCount} moves - {side} - Chose {moveToPlay} score {bestScore}, expecting {expected_response}");
    // }
}
