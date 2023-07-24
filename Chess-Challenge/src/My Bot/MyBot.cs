using System;
using System.Collections.Generic;
using ChessChallenge.API;

public class MyBot : IChessBot
{
    // Piece values: pawn, knight, bishop, rook, queen, king
    int[] pieceValues = { 100, 300, 300, 500, 900, 10000 };
    Dictionary<PieceType, int> PieceValueMap = new Dictionary<PieceType, int>
    {
        { PieceType.Pawn, 100 },
        { PieceType.Knight, 300 },
        { PieceType.Bishop, 300 },
        { PieceType.Rook, 500 },
        { PieceType.Queen, 900 },
        { PieceType.King, 10000 }
    };

    int moveCount = 0; // Track how many moves were made when searching

    // Return a int score for given board position, for the piece to play currently
    public int Evaluate(Board board)
    {
        if (board.IsInCheckmate()) { return int.MinValue; } // Lose
        else if (board.IsDraw()) { return 0; } // Draw
        // Else, count up the pieces by their values and return the sum.
        PieceList[] piecelists = board.GetAllPieceLists();

        List<Move> tracked_moves = new List<Move>(); // 
        bool didCapture;
        Move[] capture_moves;
        // Swap Material of > value or undefended until no material is left.
        // Keep doing this till no more valid captures exist.
        do
        {
            capture_moves = board.GetLegalMoves(true);
            didCapture = false;
            foreach (Move capture_move in capture_moves)
            {
                if (capture_move.IsNull) continue;
                var piece_capturing = board.GetPiece(capture_move.StartSquare);
                var piece_captured = board.GetPiece(capture_move.TargetSquare);
                // Do the first capture that improves the position.
                if (!board.SquareIsAttackedByOpponent(capture_move.TargetSquare) || // Capture for free (includes en-passants)
                    (!capture_move.IsEnPassant && PieceValueMap[piece_captured.PieceType] > PieceValueMap[piece_capturing.PieceType])) // Better trade (not en-passant)
                {
                    // Console.WriteLine($"Making capture {capture_move} {piece_capturing} takes {piece_captured} {board.GetFenString()}");
                    tracked_moves.Add(capture_move);
                    board.MakeMove(capture_move);
                    didCapture = true;
                    break;
                }
            }
        } while (didCapture); // Only keep going if a valid capture happened
                              // Note, this follows the chain of the first valid capture, it doesn't try the others,
                              // so this is not optimal, but it is simpler and okay for a first pass heuristic.

        int score = 0;
        // White pieces +, Black pieces -
        for (int i = 0; i < 6; i++)
        {
            score += piecelists[i].Count * pieceValues[i];
            score -= piecelists[i + 6].Count * pieceValues[i];
        }
        
        // Revert all the capture moves
        tracked_moves.Reverse();
        foreach (Move capture_move in tracked_moves)
        {
            board.UndoMove(capture_move);
        }

        if (board.IsInCheckmate()) { return int.MinValue; } // Lose
        else if (board.IsDraw()) { return 0; } // Draw
        // Example: More black pieces is negative if white playing, positive if black playing.
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

        foreach (Move move in allMoves)
        {
            String side = board.IsWhiteToMove ? "WHITE" : "BLACK";
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
            (int score, Move next_move, Move next_move_response) = Search(board, maxPly - 1);
            board.UndoMove(move);
            score = -score; // invert for negamax, opponents best score is our worst.
            moveCount += 1;

            // + score is better for current player
            // If the scores are equal, just randomly choose the next option sometimes.
            // This is pretty arbitrary, but mixes things up a bit
            if (score > bestScore || (score == bestScore && rng.Next(1) == 0))
            {
                moveToPlay = move;
                expected_response = next_move;
                int prevBestScore = bestScore;
                bestScore = score;
            }
        }
        return (bestScore, moveToPlay, expected_response);
    }

    public Move Think(Board board, Timer timer)
    {
        moveCount = 0;
        int maxPly = 3;
        int curScore = Evaluate(board);
        (int bestScore, Move moveToPlay, Move expected_response) = Search(board, maxPly);
        // Console.WriteLine(board.GetFenString());
        Console.WriteLine($"Searched {moveCount} moves in {timer.MillisecondsElapsedThisTurn} ms- Chose {moveToPlay} score {curScore} -> {bestScore}, expecting {expected_response}");

        return moveToPlay;
        //
        // doTest();
        // return Move.NullMove;
        //
    }

    // public void doTest() {
    //     Console.WriteLine("Testing knight move");
    //     moveCount = 0;
    //     int maxPly = 1;
    //     String test1 = "1rbqkbnr/pppppppp/8/3P4/1n2P3/2P5/PP3PPP/RNBQKBNR b KQk - 0 4";
    //     String test1 = "r2qkbnr/pppbpppp/4p3/1B1P4/Q1P2B2/2Nn1N2/P2K1PPP/R6R b k - 3 13";
    //     String test1 = "2Q2b1r/p3pppp/4k3/3R4/5B2/2N2N2/P4KPP/8 b - - 2 24"; // king walk into checkmat
    //     Board board = Board.CreateBoardFromFEN(test1);
    //     (int bestScore, Move moveToPlay, Move expected_response) = Search(board, maxPly);
    //     Console.WriteLine($"{board.GetFenString()} | Starting score {Evaluate(board)}");
    //     String side = board.IsWhiteToMove ? "WHITE" : "BLACK";
    //     Console.WriteLine($"Searched {moveCount} moves - {side} - Chose {moveToPlay} score {bestScore}, expecting {expected_response}");
    // }
}
