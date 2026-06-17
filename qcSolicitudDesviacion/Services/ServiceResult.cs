namespace qcSolicitudDesviacion.Services;

public class ServiceResult
{
    public bool Succeeded { get; set; }
    public string Message { get; set; } = string.Empty;

    public static ServiceResult Success(string message = "")
    {
        return new ServiceResult
        {
            Succeeded = true,
            Message = message
        };
    }

    public static ServiceResult Failure(string message)
    {
        return new ServiceResult
        {
            Succeeded = false,
            Message = message
        };
    }
}

public class ServiceResult<T> : ServiceResult
{
    public T? Data { get; set; }

    public static ServiceResult<T> Success(T data, string message = "")
    {
        return new ServiceResult<T>
        {
            Succeeded = true,
            Data = data,
            Message = message
        };
    }

    public new static ServiceResult<T> Failure(string message)
    {
        return new ServiceResult<T>
        {
            Succeeded = false,
            Message = message
        };
    }
}