FROM mcr.microsoft.com/dotnet/core/sdk:3.1

COPY . /project

RUN cd /project

CMD ["./build.sh", "package"]
