namespace SchoolManagementSystem.Domain.DTOs;

/// <summary>Standard API envelope: every JSON endpoint returns { succeeded, code, message, data }.</summary>
public class ResponseDto<T>
{
    public ResponseDto(T? data, int code = 200, string? message = null)
    {
        Data = data;
        Code = code;
        Message = message;
        Succeeded = code is >= 200 and < 300;
    }

    public bool Succeeded { get; set; }

    public int Code { get; set; }

    public string? Message { get; set; }

    public T? Data { get; set; }
}
