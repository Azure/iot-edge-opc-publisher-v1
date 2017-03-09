FROM microsoft/dotnet

ADD / /build/module

RUN \
		set -ex \
	&& \
		apt-get update && apt-get install -y \
			build-essential \
			libcurl4-openssl-dev \
			git \
			cmake \
			libssl-dev \
			valgrind \
			uuid-dev \
			libglib2.0-dev \
	&& \
		git clone --no-checkout https://github.com/Azure/azure-iot-gateway-sdk /build/gateway \
	&& \
        git -C /build/gateway checkout 2017-03-06 && git -C /build/gateway submodule update --init

WORKDIR /gateway
ENTRYPOINT ["sample_gateway"]
