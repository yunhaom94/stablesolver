using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace StableSolver;

// Struct to match the C API result structure
[StructLayout(LayoutKind.Sequential)]
public struct StableSolverResult
{
    public int IsFeasible;
    public int NumberOfVertices;
    public IntPtr Vertices;  // int* in C
    public double SolveTime;
}

public static partial class StableSolverInterface
{
    private const string LibraryName = "libstablesolver.so.1.0.0";
    
    // Instance builder functions
    [LibraryImport(LibraryName)]
    public static partial IntPtr create_instance_builder();

    [LibraryImport(LibraryName)]
    public static partial int instance_add_vertices(IntPtr handle, int number_of_vertices);

    [LibraryImport(LibraryName)]
    public static partial int instance_add_vertex(IntPtr handle, long weight = 1);

    [LibraryImport(LibraryName)]
    public static partial int instance_set_weight(IntPtr handle, int vertex_id, long weight);

    [LibraryImport(LibraryName)]
    public static partial int instance_add_edge(IntPtr handle, int vertex_id_1, int vertex_id_2, int check_duplicate = 0);

    [LibraryImport(LibraryName)]
    public static partial int instance_set_unweighted(IntPtr handle);

    [LibraryImport(LibraryName)]
    public static partial IntPtr instance_build(IntPtr handle);

    // Solver functions
    [LibraryImport(LibraryName)]
    public static partial StableSolverResult solve(IntPtr instance, int type);

    [LibraryImport(LibraryName)]
    public static partial void cleanup(IntPtr handle, IntPtr instance, StableSolverResult result);
}




// High-level wrapper class for easier usage
public class Solver : IDisposable
{
    private IntPtr _instanceBuilder = IntPtr.Zero;
    private IntPtr _instance = IntPtr.Zero;
    private bool _disposed = false;
    private StableSolverResult _result;

    public Solver()
    {
        _instanceBuilder = StableSolverInterface.create_instance_builder();
        if (_instanceBuilder == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create instance builder");
        }
    }

    public void AddVertices(int count)
    {
        CheckNotDisposed();
        int result = StableSolverInterface.instance_add_vertices(_instanceBuilder, count);
        if (result != 0)
        {
            throw new InvalidOperationException($"Failed to add vertices, error code: {result}");
        }
    }

    public void AddVertex(long weight = 1)
    {
        CheckNotDisposed();
        int result = StableSolverInterface.instance_add_vertex(_instanceBuilder, weight);
        if (result != 0)
        {
            throw new InvalidOperationException($"Failed to add vertex, error code: {result}");
        }
    }

    public void SetWeight(int vertexId, long weight)
    {
        CheckNotDisposed();
        int result = StableSolverInterface.instance_set_weight(_instanceBuilder, vertexId, weight);
        if (result != 0)
        {
            throw new InvalidOperationException($"Failed to set weight for vertex {vertexId}, error code: {result}");
        }
    }

    public void AddEdge(int vertex1, int vertex2, bool checkDuplicate = false)
    {
        CheckNotDisposed();
        int result = StableSolverInterface.instance_add_edge(_instanceBuilder, vertex1, vertex2, checkDuplicate ? 1 : 0);
        if (result != 0)
        {
            throw new InvalidOperationException($"Failed to add edge ({vertex1}, {vertex2}), error code: {result}");
        }
    }

    public void SetUnweighted()
    {
        CheckNotDisposed();
        int result = StableSolverInterface.instance_set_unweighted(_instanceBuilder);
        if (result != 0)
        {
            throw new InvalidOperationException($"Failed to set unweighted, error code: {result}");
        }
    }

    public void Build()
    {
        CheckNotDisposed();
        _instance = StableSolverInterface.instance_build(_instanceBuilder);
        if (_instance == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to build instance");
        }
    }

    public (bool IsFeasible, int[] Vertices, double SolveTime) Solve(int solverType = 0)
    {
        CheckNotDisposed();
        if (_instance == IntPtr.Zero)
        {
            throw new InvalidOperationException("Instance not built. Call Build() first.");
        }

        _result = StableSolverInterface.solve(_instance, solverType);
        
        List<int> vertices = new List<int>();
        unsafe
        {
            if (_result.Vertices != IntPtr.Zero)
            {
                int* vertexPtr = (int*)_result.Vertices;
                // Read vertices until we hit a negative value (assuming -1 is sentinel)
                for (int i = 0; i < _result.NumberOfVertices; i++)
                {
                    int vertex = vertexPtr[i];
                    if (vertex < 0) break;
                    vertices.Add(vertex);
                }
            }
        }

        return (_result.IsFeasible == 1, vertices.ToArray(), _result.SolveTime);
    }

    private void CheckNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(StableSolver));
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_instanceBuilder != IntPtr.Zero || _instance != IntPtr.Zero)
            {
                // Note: We create a default result for cleanup
                StableSolverInterface.cleanup(_instanceBuilder, _instance, _result);
            }
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    ~Solver()
    {
        Dispose();
    }
}